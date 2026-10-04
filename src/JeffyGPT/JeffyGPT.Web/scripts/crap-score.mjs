import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import ts from 'typescript';

// CRAP = complexity^2 * (1 - lineCoverage)^3 + complexity. Fail at 30 or higher.
export const CRAP_LIMIT = 30;

const coverageReport = path.join('coverage', 'coverage-final.json');

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  process.exitCode = scoreWorkspace();
}

export function crapScore(complexity, lineCoverage) {
  const uncovered = 1 - lineCoverage;
  return complexity * complexity * uncovered * uncovered * uncovered + complexity;
}

export function scoreSource(sourceText, filePath, statements) {
  const scriptKind = filePath.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS;
  const sourceFile = ts.createSourceFile(
    filePath,
    sourceText,
    ts.ScriptTarget.Latest,
    true,
    scriptKind,
  );
  const functions = [];
  collectFunctions(sourceFile, functions);

  return functions.map((fn) => {
    const complexity = cyclomaticComplexity(fn);
    const coverage = lineCoverageForFunction(sourceFile, fn, statements);
    return {
      file: filePath,
      name: functionLabel(fn, sourceFile),
      complexity,
      coverage,
      score: crapScore(complexity, coverage),
    };
  });
}

function scoreWorkspace() {
  if (!fs.existsSync(coverageReport)) {
    console.error(`Coverage report not found: ${coverageReport}`);
    return 1;
  }

  const coverage = JSON.parse(fs.readFileSync(coverageReport, 'utf8'));
  const statementsByFile = indexStatements(coverage);
  const scored = [];

  for (const filePath of listSourceFiles('src')) {
    const sourceText = fs.readFileSync(filePath, 'utf8');
    const statements = lookupStatements(statementsByFile, filePath);
    scored.push(...scoreSource(sourceText, filePath, statements));
  }

  const offenders = scored
    .filter((method) => method.score >= CRAP_LIMIT)
    .sort(
      (left, right) =>
        right.score - left.score ||
        left.file.localeCompare(right.file) ||
        left.name.localeCompare(right.name),
    );

  for (const method of offenders) {
    const displayFile = path.relative(process.cwd(), method.file).replaceAll('\\', '/');
    console.log(
      `CRAP ${formatScore(method.score)} complexity ${method.complexity} coverage ${formatPercent(method.coverage)} ${displayFile} ${method.name}`,
    );
  }

  if (offenders.length === 0) {
    console.log(`CRAP check passed. ${scored.length} method(s) scored.`);
    return 0;
  }

  console.error(`CRAP check failed. ${offenders.length} method(s) scored ${CRAP_LIMIT} or higher.`);
  return 1;
}

function listSourceFiles(root) {
  return fs
    .readdirSync(root, { recursive: true })
    .map((entry) => String(entry).replaceAll('\\', '/'))
    .filter((entry) => isProductionSource(entry))
    .map((entry) => path.join(root, entry));
}

function isProductionSource(relativePath) {
  if (
    relativePath.endsWith('.d.ts') ||
    relativePath.endsWith('.test.ts') ||
    relativePath.endsWith('.test.tsx')
  ) {
    return false;
  }

  return relativePath.endsWith('.ts') || relativePath.endsWith('.tsx');
}

function indexStatements(coverage) {
  const indexed = new Map();
  for (const [filePath, fileCoverage] of Object.entries(coverage)) {
    const statementMap = fileCoverage.statementMap ?? {};
    const hits = fileCoverage.s ?? {};
    const statements = Object.keys(statementMap).map((id) => ({
      line: statementMap[id].start.line,
      column: statementMap[id].start.column,
      hits: hits[id] ?? 0,
    }));
    indexed.set(normalizePath(filePath), statements);
  }

  return indexed;
}

function lookupStatements(indexed, filePath) {
  return indexed.get(normalizePath(filePath)) ?? null;
}

function normalizePath(filePath) {
  return path.resolve(filePath).replaceAll('\\', '/').toLowerCase();
}

function collectFunctions(node, functions) {
  if (isFunctionLike(node)) {
    functions.push(node);
  }

  ts.forEachChild(node, (child) => collectFunctions(child, functions));
}

function cyclomaticComplexity(fn) {
  let complexity = 1;

  function visit(node) {
    if (node !== fn && isFunctionLike(node)) {
      return;
    }

    complexity += decisionIncrement(node);
    ts.forEachChild(node, visit);
  }

  visit(fn);
  return complexity;
}

function lineCoverageForFunction(sourceFile, fn, statements) {
  if (statements === null) {
    return 0;
  }

  const nested = [];
  function visit(node) {
    if (node !== fn && isFunctionLike(node)) {
      nested.push(node);
      return;
    }

    ts.forEachChild(node, visit);
  }

  visit(fn);

  let total = 0;
  let hit = 0;
  for (const statement of statements) {
    const position = positionOf(sourceFile, statement.line, statement.column);
    if (position < 0 || !containsPosition(fn, sourceFile, position)) {
      continue;
    }

    if (nested.some((inner) => containsPosition(inner, sourceFile, position))) {
      continue;
    }

    total += 1;
    if (statement.hits > 0) {
      hit += 1;
    }
  }

  return total === 0 ? 1 : hit / total;
}

function positionOf(sourceFile, line, column) {
  const lineIndex = line - 1;
  if (lineIndex < 0 || lineIndex >= sourceFile.getLineStarts().length) {
    return -1;
  }

  const lineStart = sourceFile.getLineStarts()[lineIndex];
  const lineEnd = sourceFile.getLineEndOfPosition(lineStart);
  const offset = Math.min(column, lineEnd - lineStart);
  return lineStart + offset;
}

function containsPosition(node, sourceFile, position) {
  return position >= node.getStart(sourceFile) && position < node.getEnd();
}

function decisionIncrement(node) {
  switch (node.kind) {
    case ts.SyntaxKind.IfStatement:
    case ts.SyntaxKind.ForStatement:
    case ts.SyntaxKind.ForInStatement:
    case ts.SyntaxKind.ForOfStatement:
    case ts.SyntaxKind.WhileStatement:
    case ts.SyntaxKind.DoStatement:
    case ts.SyntaxKind.CatchClause:
    case ts.SyntaxKind.ConditionalExpression:
    case ts.SyntaxKind.CaseClause:
      return 1;
    case ts.SyntaxKind.BinaryExpression: {
      const operator = node.operatorToken.kind;
      if (
        operator === ts.SyntaxKind.AmpersandAmpersandToken ||
        operator === ts.SyntaxKind.BarBarToken ||
        operator === ts.SyntaxKind.QuestionQuestionToken
      ) {
        return 1;
      }

      return 0;
    }
    default:
      return 0;
  }
}

function isFunctionLike(node) {
  return (
    ts.isFunctionDeclaration(node) ||
    ts.isFunctionExpression(node) ||
    ts.isArrowFunction(node) ||
    ts.isMethodDeclaration(node) ||
    ts.isConstructorDeclaration(node) ||
    ts.isGetAccessorDeclaration(node) ||
    ts.isSetAccessorDeclaration(node)
  );
}

function functionLabel(node, sourceFile) {
  const line = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
  if (ts.isConstructorDeclaration(node)) {
    const parent = className(node.parent);
    return parent ? `${parent}.constructor` : `constructor:${line}`;
  }

  if (
    (ts.isMethodDeclaration(node) ||
      ts.isGetAccessorDeclaration(node) ||
      ts.isSetAccessorDeclaration(node)) &&
    node.name &&
    ts.isIdentifier(node.name)
  ) {
    const prefix = ts.isGetAccessorDeclaration(node)
      ? 'get '
      : ts.isSetAccessorDeclaration(node)
        ? 'set '
        : '';
    const name = `${prefix}${node.name.text}`;
    const parent = className(node.parent);
    return parent ? `${parent}.${name}` : name;
  }

  if ((ts.isFunctionDeclaration(node) || ts.isFunctionExpression(node)) && node.name) {
    return node.name.text;
  }

  if (ts.isArrowFunction(node) || ts.isFunctionExpression(node)) {
    const parent = node.parent;
    if (ts.isVariableDeclaration(parent) && ts.isIdentifier(parent.name)) {
      return parent.name.text;
    }

    if (ts.isPropertyAssignment(parent) && ts.isIdentifier(parent.name)) {
      return parent.name.text;
    }
  }

  return `anonymous:${line}`;
}

function className(node) {
  if ((ts.isClassDeclaration(node) || ts.isClassExpression(node)) && node.name) {
    return node.name.text;
  }

  return null;
}

function formatScore(score) {
  const rounded = Math.round(score * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
}

function formatPercent(coverage) {
  return `${Math.round(coverage * 100)}%`;
}
