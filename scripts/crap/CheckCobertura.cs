using System.Globalization;
using System.Xml.Linq;

// CRAP = complexity^2 * (1 - lineCoverage)^3 + complexity. Fail at 30 or higher.
const double CrapLimit = 30d;

if (args is ["--self-test"])
{
    return RunSelfTest();
}

var directory = args.Length == 0 ? "artifacts/coverage" : args[0];
return ScoreDirectory(directory);

static int RunSelfTest()
{
    var failures = new List<string>();
    Expect(Close(CrapMath.Score(5, 0), 30), "complexity 5 with no coverage scores 30", failures);
    Expect(Close(CrapMath.Score(4, 0), 20), "complexity 4 with no coverage scores 20", failures);
    Expect(Close(CrapMath.Score(31, 1), 31), "fully covered complexity 31 scores 31", failures);
    Expect(Close(CrapMath.Score(10, 0.5), 22.5), "complexity 10 at 50% coverage scores 22.5", failures);

    var methods = LoadDirectory(Path.Combine("scripts", "crap", "fixtures"));
    Expect(methods.Count == 5, "compiler-generated method is skipped and the rest are scored once", failures);

    var merged = Find(methods, "Sample.Widget.Merged");
    Expect(merged is { Complexity: 10 } && Close(merged.Coverage, 1) && Close(merged.Score, 10),
        "split line hits merge to full coverage",
        failures);

    var half = Find(methods, "Sample.Widget.Half");
    Expect(half is { Complexity: 10 } && Close(half.Coverage, 0.5) && Close(half.Score, 22.5),
        "half-covered method scores 22.5",
        failures);

    var untested = Find(methods, "Sample.Widget.Untested");
    Expect(untested is not null && Close(untested.Score, 30), "untested complexity 5 scores 30", failures);

    var simple = Find(methods, "Sample.Widget.Simple");
    Expect(simple is not null && Close(simple.Score, 20), "untested complexity 4 scores 20", failures);

    var covered = Find(methods, "Sample.Widget.CoveredComplex");
    Expect(covered is not null && Close(covered.Score, 31), "fully covered complexity 31 scores 31", failures);

    Expect(!methods.Any(method => method.Name.Contains('<', StringComparison.Ordinal)),
        "compiler-generated method name is absent",
        failures);

    var offenders = methods.Where(method => method.Score >= CrapLimit).Select(method => method.Name).Order(StringComparer.Ordinal).ToArray();
    Expect(offenders.SequenceEqual(["Sample.Widget.CoveredComplex", "Sample.Widget.Untested"]),
        "only methods at or above 30 are offenders",
        failures);

    if (failures.Count > 0)
    {
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return 1;
    }

    Console.WriteLine("CRAP self-test passed.");
    return 0;
}

static int ScoreDirectory(string directory)
{
    List<ScoredMethod> methods;
    try
    {
        methods = LoadDirectory(directory);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    var offenders = methods.Where(method => method.Score >= CrapLimit)
        .OrderByDescending(method => method.Score)
        .ThenBy(method => method.File, StringComparer.Ordinal)
        .ThenBy(method => method.Name, StringComparer.Ordinal)
        .ToList();

    foreach (var method in offenders)
    {
        Console.WriteLine(FormattableString.Invariant(
            $"CRAP {FormatScore(method.Score)} complexity {method.Complexity} coverage {FormatPercent(method.Coverage)} {method.File} {method.Name}"));
    }

    if (offenders.Count == 0)
    {
        Console.WriteLine(FormattableString.Invariant($"CRAP check passed. {methods.Count} method(s) scored."));
        return 0;
    }

    Console.Error.WriteLine(FormattableString.Invariant(
        $"CRAP check failed. {offenders.Count} method(s) scored {CrapLimit.ToString("0", CultureInfo.InvariantCulture)} or higher."));
    return 1;
}

static List<ScoredMethod> LoadDirectory(string directory)
{
    if (!Directory.Exists(directory))
    {
        throw new DirectoryNotFoundException($"Coverage directory not found: {directory}");
    }

    var files = Directory.EnumerateFiles(directory, "coverage.cobertura.xml", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .ToList();
    if (files.Count == 0)
    {
        throw new FileNotFoundException($"No coverage.cobertura.xml files under {directory}.");
    }

    var merged = new Dictionary<string, MutableMethod>(StringComparer.Ordinal);
    foreach (var file in files)
    {
        var document = XDocument.Load(file);
        foreach (var classElement in document.Descendants().Where(element => element.Name.LocalName == "class"))
        {
            var className = classElement.Attribute("name")?.Value ?? "";
            var fileName = (classElement.Attribute("filename")?.Value ?? "").Replace('\\', '/');
            var methodsElement = classElement.Elements().FirstOrDefault(element => element.Name.LocalName == "methods");
            if (methodsElement is null)
            {
                continue;
            }

            foreach (var methodElement in methodsElement.Elements().Where(element => element.Name.LocalName == "method"))
            {
                var methodName = methodElement.Attribute("name")?.Value ?? "";
                if (methodName.Contains('<', StringComparison.Ordinal))
                {
                    continue;
                }

                if (!int.TryParse(methodElement.Attribute("complexity")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var complexity)
                    || complexity <= 0)
                {
                    continue;
                }

                var signature = methodElement.Attribute("signature")?.Value ?? "";
                var key = string.Join("|", fileName, className, methodName, signature);
                if (!merged.TryGetValue(key, out var method))
                {
                    method = new MutableMethod(fileName, DisplayName(className, methodName));
                    merged.Add(key, method);
                }

                method.Complexity = Math.Max(method.Complexity, complexity);
                var linesElement = methodElement.Elements().FirstOrDefault(element => element.Name.LocalName == "lines");
                if (linesElement is null)
                {
                    continue;
                }

                foreach (var lineElement in linesElement.Elements().Where(element => element.Name.LocalName == "line"))
                {
                    if (!int.TryParse(lineElement.Attribute("number")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        continue;
                    }

                    var hits = 0;
                    _ = int.TryParse(lineElement.Attribute("hits")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out hits);
                    method.Hits.TryGetValue(number, out var existing);
                    method.Hits[number] = existing + Math.Max(hits, 0);
                }
            }
        }
    }

    return merged.Values.Select(method => method.ToScored()).OrderBy(method => method.Name, StringComparer.Ordinal).ToList();
}

static string DisplayName(string className, string methodName)
{
    return className.Length == 0 ? methodName : className + "." + methodName;
}

static ScoredMethod? Find(IReadOnlyList<ScoredMethod> methods, string name)
{
    return methods.FirstOrDefault(method => method.Name == name);
}

static void Expect(bool condition, string message, List<string> failures)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

static bool Close(double actual, double expected)
{
    return Math.Abs(actual - expected) < 0.001;
}

static string FormatScore(double score)
{
    var rounded = Math.Round(score, 1, MidpointRounding.AwayFromZero);
    var whole = Math.Round(rounded, 0, MidpointRounding.AwayFromZero);
    if (Math.Abs(rounded - whole) < 0.001)
    {
        return whole.ToString("0", CultureInfo.InvariantCulture);
    }

    return rounded.ToString("0.0", CultureInfo.InvariantCulture);
}

static string FormatPercent(double coverage)
{
    return Math.Round(coverage * 100d, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
}

sealed class MutableMethod(string file, string name)
{
    public string File { get; } = file;

    public string Name { get; } = name;

    public int Complexity { get; set; }

    public Dictionary<int, int> Hits { get; } = [];

    public ScoredMethod ToScored()
    {
        var coverage = Hits.Count == 0 ? 1d : Hits.Values.Count(hits => hits > 0) / (double)Hits.Count;
        return new ScoredMethod(File, Name, Complexity, coverage, CrapMath.Score(Complexity, coverage));
    }
}

sealed record ScoredMethod(string File, string Name, int Complexity, double Coverage, double Score);

static class CrapMath
{
    public static double Score(int complexity, double lineCoverage)
    {
        var uncovered = 1d - lineCoverage;
        return complexity * complexity * uncovered * uncovered * uncovered + complexity;
    }
}
