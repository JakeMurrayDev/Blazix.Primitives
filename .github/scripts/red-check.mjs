// ADR 11 section 3, the red check: a test added by a pull request must fail against the base branch's src/.
//
//   node .github/scripts/red-check.mjs --base-sha <sha> --base-dir <checkout of that sha> [--layers l1,l2,generators,l3,l4]
//
// Run at the root of the pull request's head checkout, after `npm ci`. For each layer it lists the tests in the base
// checkout and in the head checkout, takes the names the head added, swaps the head checkout's src/ for the base's
// (the tests/ and fixtures stay the pull request's), runs the added tests at their layer, and fails when one passes.
// A layer whose test sources did not change is not even listed; a pull request that adds no test passes at once.
//
// The first half of the file is pure and unit-tested (red-check.test.mjs); the second half drives the tools.
import { execFileSync, spawnSync } from 'node:child_process';
import { appendFileSync, existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { join, relative } from 'node:path';
import { pathToFileURL } from 'node:url';

const decodeEntities = (text) =>
  text.replace(/&(quot|apos|lt|gt|amp|#(\d+)|#x([0-9a-fA-F]+));/g, (_, name, decimal, hex) => {
    if (decimal) return String.fromCodePoint(Number(decimal));
    if (hex) return String.fromCodePoint(parseInt(hex, 16));
    return { quot: '"', apos: "'", lt: '<', gt: '>', amp: '&' }[name];
  });

// ---- listings ----------------------------------------------------------------------------------------------------

/** @param {string} text the output of `dotnet test --list-tests json`, which may start with progress lines */
export function parseDotnetList(text) {
  const start = text.indexOf('{');
  const end = text.lastIndexOf('}');
  const report = JSON.parse(text.slice(start, end + 1));
  return report.testContainers.flatMap((container) => container.tests.map((test) => test.displayName));
}

/**
 * @param {string} text vitest's JSON reporter output @param {string} root the repository root
 * Names come from a run, not from `vitest list`, which shows an it.each title once with its placeholder.
 */
export function parseVitestList(text, root) {
  return [...vitestOutcomes(text, root).keys()];
}

function* playwrightSpecs(suite, titles) {
  for (const spec of suite.specs ?? []) yield { spec, titles: [...titles, spec.title] };
  for (const child of suite.suites ?? []) yield* playwrightSpecs(child, [...titles, child.title]);
}

/** @param {string} text the output of `playwright test --list --reporter=json`, which has one spec per project */
export function parsePlaywrightList(text) {
  const report = JSON.parse(text);
  const keys = new Set();
  for (const fileSuite of report.suites ?? []) {
    for (const { titles } of playwrightSpecs(fileSuite, [])) {
      keys.add(`tests/browser/${fileSuite.file}::${titles.join(' > ')}`);
    }
  }
  return [...keys];
}

/** The names `head` has more of than `base`: a multiset difference that keeps the head's order. */
export function addedNames(base, head) {
  const remaining = new Map();
  for (const name of base) remaining.set(name, (remaining.get(name) ?? 0) + 1);
  const added = [];
  for (const name of head) {
    const count = remaining.get(name) ?? 0;
    if (count > 0) remaining.set(name, count - 1);
    else added.push(name);
  }
  return added;
}

/**
 * Lint tests read the repository (documents, imports, source text) and are green on the base branch by
 * construction; their red run is the planted violation the pull request shows. They live in a `Lints` namespace (L1,
 * L2) or a `*.lint.test.ts` file (L3), which is what the check recognises.
 */
export function isExempt(layer, name) {
  if (layer === 'l3') return name.split('::')[0]?.endsWith('.lint.test.ts') ?? false;
  if (layer === 'l4') return false;
  return /\.Lints\./.test(name);
}

// ---- outcomes ----------------------------------------------------------------------------------------------------

/** @param {string} xml a JUnit report written by `--report-xunit-junit` @returns {Map<string, 'passed'|'failed'|'skipped'>} */
export function dotnetOutcomes(xml) {
  const outcomes = new Map();
  for (const match of xml.matchAll(/<testcase\b([^>]*?)(\/>|>([\s\S]*?)<\/testcase>)/g)) {
    const name = /\bname="([^"]*)"/.exec(match[1] ?? '')?.[1];
    if (name === undefined) continue;
    const body = match[3] ?? '';
    const status = /<(failure|error)\b/.test(body) ? 'failed' : /<skipped\b/.test(body) ? 'skipped' : 'passed';
    outcomes.set(decodeEntities(name), status);
  }
  return outcomes;
}

/** @param {string} text vitest's JSON reporter output @param {string} root the repository root */
export function vitestOutcomes(text, root) {
  const outcomes = new Map();
  for (const file of JSON.parse(text).testResults ?? []) {
    for (const test of file.assertionResults ?? []) {
      const key = `${relative(root, file.name)}::${[...test.ancestorTitles, test.title].join(' > ')}`;
      outcomes.set(key, test.status === 'passed' ? 'passed' : test.status === 'failed' ? 'failed' : 'skipped');
    }
  }
  return outcomes;
}

/**
 * Files vitest could not load (an import that does not resolve): a failed suite with no per-test results. Against the
 * base's src/ this is what a test file for a module the pull request adds looks like.
 * @param {string} text vitest's JSON reporter output @param {string} root the repository root
 */
export function vitestFailedFiles(text, root) {
  return (JSON.parse(text).testResults ?? [])
    .filter((file) => file.status === 'failed' && (file.assertionResults ?? []).length === 0)
    .map((file) => relative(root, file.name));
}

const rank = { passed: 2, failed: 1, skipped: 0 };

/** A test that passes in any project counts as passed; otherwise one that failed counts as failed. */
export function playwrightOutcomes(text) {
  const outcomes = new Map();
  for (const fileSuite of JSON.parse(text).suites ?? []) {
    for (const { spec, titles } of playwrightSpecs(fileSuite, [])) {
      const statuses = spec.tests.map((test) => test.results.at(-1)?.status ?? 'skipped');
      const status = statuses.includes('passed') ? 'passed' : statuses.some((s) => s !== 'skipped') ? 'failed' : 'skipped';
      const key = `tests/browser/${fileSuite.file}::${titles.join(' > ')}`;
      // One spec per project: keep the best result of the projects.
      const known = outcomes.get(key);
      if (known === undefined || rank[known] < rank[status]) outcomes.set(key, status);
    }
  }
  return outcomes;
}

// ---- verdict -----------------------------------------------------------------------------------------------------

/**
 * @param {string[]} added the added test names of one layer
 * @param {Map<string, string>} outcomes what running them against the base branch's src/ produced
 * @param {{ buildFailed?: boolean, failedFiles?: string[] }} [options] a layer that does not build against the base has
 *   every added test red; so does every added test of a vitest file that does not load (`failedFiles`)
 */
export function judge(added, outcomes, { buildFailed = false, failedFiles = [] } = {}) {
  const verdict = { red: [], unloadable: [], passed: [], skipped: [], notRun: [], ok: true };
  for (const name of added) {
    const outcome = buildFailed ? 'failed' : outcomes.get(name);
    if (outcome === undefined && failedFiles.includes(name.split('::')[0] ?? '')) {
      verdict.red.push(name);
      verdict.unloadable.push(name);
    } else if (outcome === 'failed') verdict.red.push(name);
    else if (outcome === 'passed') verdict.passed.push(name);
    else if (outcome === 'skipped') verdict.skipped.push(name);
    else verdict.notRun.push(name);
  }
  verdict.ok = verdict.passed.length === 0 && verdict.notRun.length === 0;
  return verdict;
}

const names = (list) => list.map((name) => `- \`${name}\``).join('\n');

/** @param {{ layer: string, added: string[], exempt: string[], verdict?: ReturnType<typeof judge> }[]} layers */
export function summarize(layers) {
  const lines = ['### Red check', ''];
  if (layers.every((layer) => layer.added.length === 0)) {
    const exempt = layers.reduce((total, layer) => total + layer.exempt.length, 0);
    lines.push(`No test was added, so there is nothing to run against the base branch.${exempt > 0 ? ` (${exempt} lint test(s) are exempt.)` : ''}`);
    return { markdown: lines.join('\n') + '\n', ok: true };
  }
  lines.push('| layer | added | red | passes |', '|---|---|---|---|');
  let ok = true;
  for (const { layer, added, verdict } of layers) {
    if (added.length === 0 || !verdict) continue;
    lines.push(`| ${layer} | ${added.length} | ${verdict.red.length} | ${verdict.passed.length} |`);
    ok &&= verdict.ok;
  }
  for (const { layer, exempt, verdict } of layers) {
    if (verdict && verdict.passed.length > 0) {
      lines.push('', `**${layer}: each of these passes against the base branch's src/, so it proves nothing about the change:**`, names(verdict.passed));
    }
    const unloadable = verdict?.unloadable ?? [];
    if (unloadable.length > 0) {
      const perFile = new Map();
      for (const name of unloadable) perFile.set(name.split('::')[0], (perFile.get(name.split('::')[0]) ?? 0) + 1);
      lines.push(
        '',
        `**${layer}: red: the test file does not load against the base's src/ (it imports a module the pull request adds), so its tests fail there:**`,
        [...perFile].map(([file, count]) => `- \`${file}\` (${count} tests)`).join('\n'),
      );
    }
    if (verdict && verdict.notRun.length > 0) {
      lines.push('', `**${layer}: these tests did not run against the base branch's src/ (a filter or host problem), so they cannot be shown red:**`, names(verdict.notRun));
    }
    if (exempt.length > 0) lines.push('', `${layer}: ${exempt.length} lint test(s) exempt (they verify the repository, not the base branch's src/).`);
  }
  lines.push('', ok ? 'Every added test fails against the base branch.' : 'Show each test failing with its fix reverted before it counts (ADR 06 section 1, ADR 11 section 3).');
  return { markdown: lines.join('\n') + '\n', ok };
}

// ---- driving the tools -------------------------------------------------------------------------------------------

const layers = {
  l1: { kind: 'dotnet', project: 'tests/Blazix.Primitives.Tests', changedUnder: ['tests/Blazix.Primitives.Tests/'] },
  l2: { kind: 'dotnet', project: 'tests/Blazix.Primitives.Tests.Components', changedUnder: ['tests/Blazix.Primitives.Tests.Components/'] },
  generators: { kind: 'dotnet', project: 'tests/Blazix.Primitives.Tests.Generators', changedUnder: ['tests/Blazix.Primitives.Tests.Generators/'] },
  l3: { kind: 'vitest', changedUnder: ['src/Blazix.Primitives/js/'], changedSuffix: '.test.ts' },
  l4: { kind: 'playwright', changedUnder: ['tests/browser/'] },
};

function run(command, args, { cwd = process.cwd(), allowFailure = false } = {}) {
  const result = spawnSync(command, args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
  if (!allowFailure && result.status !== 0) {
    throw new Error(`${command} ${args.slice(0, 4).join(' ')} failed (${result.status}):\n${result.stdout.slice(-2000)}\n${result.stderr.slice(-2000)}`);
  }
  return result;
}

function dotnetBuild(project, cwd) {
  const result = run('dotnet', ['build', project, '-c', 'Release', '-v', 'q', '-nologo'], { cwd, allowFailure: true });
  return result.status === 0;
}

function listLayer(layer, cwd) {
  const config = layers[layer];
  if (config.kind === 'dotnet') {
    if (!dotnetBuild(config.project, cwd)) throw new Error(`${config.project} does not build at ${cwd}`);
    return parseDotnetList(run('dotnet', ['test', '--project', config.project, '-c', 'Release', '--no-build', '--list-tests', 'json'], { cwd }).stdout);
  }
  if (config.kind === 'vitest') {
    const report = join(cwd, 'red-check-list.json');
    run('npx', ['vitest', 'run', 'src/Blazix.Primitives/js', '--reporter=json', `--outputFile=${report}`], { cwd, allowFailure: true });
    const names = existsSync(report) ? parseVitestList(readFileSync(report, 'utf8'), cwd) : [];
    rmSync(report, { force: true });
    return names;
  }
  return parsePlaywrightList(run('npx', ['playwright', 'test', '--list', '--reporter=json'], { cwd }).stdout);
}

function runLayer(layer, added, resultsDir) {
  const config = layers[layer];
  const cwd = process.cwd();
  mkdirSync(resultsDir, { recursive: true });
  if (config.kind === 'dotnet') {
    if (!dotnetBuild(config.project, cwd)) return { buildFailed: true, outcomes: new Map() };
    const outcomes = new Map();
    for (let i = 0; i < added.length; i += 200) {
      const file = `${layer}-${i}.xml`;
      const filters = added.slice(i, i + 200).flatMap((name) => ['--filter-display-name', name]);
      run('dotnet', ['test', '--project', config.project, '-c', 'Release', '--no-build', ...filters, '--report-xunit-junit', '--report-xunit-junit-filename', file, '--results-directory', resultsDir], { allowFailure: true });
      const report = join(resultsDir, file);
      if (existsSync(report)) for (const [name, status] of dotnetOutcomes(readFileSync(report, 'utf8'))) outcomes.set(name, status);
    }
    return { buildFailed: false, outcomes };
  }
  if (config.kind === 'vitest') {
    const report = join(resultsDir, `${layer}.json`);
    run('npx', ['vitest', 'run', 'src/Blazix.Primitives/js', '--reporter=json', `--outputFile=${report}`], { allowFailure: true });
    if (!existsSync(report)) return { buildFailed: false, outcomes: new Map(), failedFiles: [] };
    const text = readFileSync(report, 'utf8');
    return { buildFailed: false, outcomes: vitestOutcomes(text, cwd), failedFiles: vitestFailedFiles(text, cwd) };
  }
  // The fixture host is part of what the base branch's src/ must build for.
  if (!dotnetBuild('tests/Blazix.Primitives.Fixtures', cwd)) return { buildFailed: true, outcomes: new Map() };
  const files = [...new Set(added.map((key) => key.split('::')[0]))];
  const report = join(resultsDir, `${layer}.json`);
  spawnSync('npx', ['playwright', 'test', ...files, '--reporter=json'], {
    cwd,
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
    stdio: 'inherit',
    env: { ...process.env, PLAYWRIGHT_JSON_OUTPUT_NAME: report },
  });
  return { buildFailed: false, outcomes: existsSync(report) ? playwrightOutcomes(readFileSync(report, 'utf8')) : new Map() };
}

function main() {
  const options = {};
  for (let i = 2; i < process.argv.length; i += 2) options[process.argv[i].replace(/^--/, '')] = process.argv[i + 1];
  const baseSha = options['base-sha'];
  const baseDir = options['base-dir'];
  if (!baseSha || !baseDir) throw new Error('usage: red-check.mjs --base-sha <sha> --base-dir <dir> [--layers l1,l2,...]');
  const selected = (options['layers'] ?? Object.keys(layers).join(',')).split(',');
  const changed = execFileSync('git', ['diff', '--name-only', baseSha, 'HEAD'], { encoding: 'utf8' }).split('\n').filter(Boolean);

  const results = [];
  for (const layer of selected) {
    const config = layers[layer];
    const touched = changed.some((path) => config.changedUnder.some((prefix) => path.startsWith(prefix)) && (!config.changedSuffix || path.endsWith(config.changedSuffix)));
    if (!touched) {
      results.push({ layer, added: [], exempt: [] });
      continue;
    }
    const all = addedNames(listLayer(layer, baseDir), listLayer(layer, process.cwd()));
    results.push({ layer, added: all.filter((name) => !isExempt(layer, name)), exempt: all.filter((name) => isExempt(layer, name)) });
  }

  if (results.some((result) => result.added.length > 0)) {
    // The base branch's src/, the pull request's tests/ and fixtures. The L3 tests sit beside the modules under src/, so
    // the pull request's *.test.ts files under js/ are put back after the swap.
    rmSync('src', { recursive: true, force: true });
    execFileSync('git', ['checkout', baseSha, '--', 'src']);
    spawnSync('git', ['checkout', 'HEAD', '--', ':(glob)src/Blazix.Primitives/js/**/*.test.ts'], { stdio: 'ignore' });
    for (const result of results) {
      if (result.added.length === 0) continue;
      const { buildFailed, outcomes, failedFiles } = runLayer(result.layer, result.added, join(process.cwd(), 'red-check-results'));
      result.verdict = judge(result.added, outcomes, { buildFailed, failedFiles });
    }
  }

  const { markdown, ok } = summarize(results);
  if (process.env['GITHUB_STEP_SUMMARY']) appendFileSync(process.env['GITHUB_STEP_SUMMARY'], markdown);
  process.stdout.write(markdown);
  process.exit(ok ? 0 : 1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
