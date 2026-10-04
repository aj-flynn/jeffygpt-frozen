namespace JeffyAspire.Hosting;

internal sealed record AssistantRunSettings(string ApiLaunchProfile, string? AssistantProvider)
{
    public const string HttpProfile = "http";
    public const string HttpOpenAiProfile = "http-openai";
    public const string OpenAiCompatibleProvider = "OpenAiCompatible";
    public const string MarkerFileName = ".run-assistant-provider";
    public static readonly TimeSpan MarkerMaxAge = TimeSpan.FromMinutes(15);

    public static AssistantRunSettings Resolve(string appHostDirectory)
    {
        return Resolve(
            Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE"),
            ReadAssistantProviderMarker(appHostDirectory),
            Environment.GetEnvironmentVariable("JeffyGPT__Assistant__Provider"));
    }

    internal static AssistantRunSettings Resolve(
        string? dotnetLaunchProfile,
        string? markerProvider,
        string? envProvider)
    {
        var apiLaunchProfile = IsHttpOpenAiProfile(dotnetLaunchProfile) || IsOpenAiCompatible(markerProvider)
            ? HttpOpenAiProfile
            : HttpProfile;

        if (IsHttpOpenAiProfile(apiLaunchProfile))
        {
            return new(apiLaunchProfile, OpenAiCompatibleProvider);
        }

        if (!string.IsNullOrWhiteSpace(markerProvider))
        {
            return new(apiLaunchProfile, markerProvider);
        }

        return new(apiLaunchProfile, string.IsNullOrWhiteSpace(envProvider) ? null : envProvider);
    }

    internal static bool IsHttpOpenAiProfile(string? profile) =>
        string.Equals(profile, HttpOpenAiProfile, StringComparison.OrdinalIgnoreCase);

    internal static bool IsOpenAiCompatible(string? provider) =>
        string.Equals(provider, OpenAiCompatibleProvider, StringComparison.OrdinalIgnoreCase);

    internal static bool IsStaleMarker(DateTime lastWriteTimeUtc, DateTime utcNow) =>
        utcNow - lastWriteTimeUtc > MarkerMaxAge;

    private static string? ReadAssistantProviderMarker(string appHostDirectory)
    {
        var markerPath = Path.Combine(appHostDirectory, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            return null;
        }

        // Ignore leftovers from crashed debug sessions so plain `dotnet run` / Fake stays Fake.
        if (IsStaleMarker(File.GetLastWriteTimeUtc(markerPath), DateTime.UtcNow))
        {
            try
            {
                File.Delete(markerPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; treat as absent.
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        var value = File.ReadAllText(markerPath).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
