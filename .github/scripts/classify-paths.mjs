// ADR 11 section 1: decides from a pull request's changed paths which jobs have work, and which L4 specs run.
//
//   node .github/scripts/classify-paths.mjs --event pull_request --base <sha> --head <sha> --labels "a,b"
//
// Writes the keys below to $GITHUB_OUTPUT (stdout when unset) and a table to $GITHUB_STEP_SUMMARY.
//
//   docs      a path under docs/ changed                         -> docs-lint
//   code      a path outside docs/ changed                       -> build-and-test
//   l4        none | scoped | full
//   run-l4    true when the browser jobs have specs to run
//   families  the families a scoped run covers (space-separated)
//   specs     the spec files of a scoped run (space-separated); empty for a full run, which runs every spec
//
// The rule, in the order the code applies it:
//   a push to main, or the label full-l4            -> full
//   any shared path (the list below, or unknown)    -> full, so a new folder is never silently skipped
//   only family paths and harness specs             -> scoped to those specs
//   only docs, Markdown, the L1/L2/generator tests and samples/ -> none (nothing a browser can observe)
import { appendFileSync, existsSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { basename } from 'node:path';
import { pathToFileURL } from 'node:url';

const libraryRoot = 'src/Blazix.Primitives/';
const browserRoot = 'tests/browser/';
// Paths that Playwright never builds or runs against: a change confined to them cannot change a browser result. The three
// test projects have tests of their own; samples/ is the sample application (ADR 09, Amendments 2026-10-22), which
// build-and-test builds and no spec visits.
const inertPrefixes = [
  'tests/Blazix.Primitives.Tests/',
  'tests/Blazix.Primitives.Tests.Components/',
  'tests/Blazix.Primitives.Tests.Generators/',
  'samples/',
];

/** @param {string} stem a document stem such as `alert-dialog` @returns {string} the family folder, `AlertDialog` (ADR 08) */
export function familyFolderName(stem) {
  return stem
    .split('-')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join('');
}

/** @param {string} fileName a file directly under tests/browser @returns {string} the name before the first dot */
const specOwner = (fileName) => fileName.split('.')[0] ?? fileName;

/**
 * @param {{ changed: string[], labels: string[], event: string, families: string[], specFiles: string[] }} input
 *   `families` are the stems of docs/components/*.md; `specFiles` are the files of tests/browser at the PR's head.
 */
export function classify({ changed, labels, event, families, specFiles }) {
  const familyByFolder = new Map(families.map((stem) => [familyFolderName(stem), stem]));
  const familySet = new Set(families);

  let docs = false;
  let code = false;
  const shared = [];
  const touchedFamilies = new Set();
  const harnessSpecs = new Set();

  for (const path of changed) {
    if (path.startsWith('docs/')) {
      docs = true;
      continue;
    }
    code = true;
    if (path.endsWith('.md') || path === 'LICENSE' || inertPrefixes.some((prefix) => path.startsWith(prefix))) {
      continue;
    }
    if (path.startsWith(libraryRoot)) {
      const [folder, ...rest] = path.slice(libraryRoot.length).split('/');
      const family = rest.length > 0 ? familyByFolder.get(folder ?? '') : undefined;
      if (family) {
        touchedFamilies.add(family);
        continue;
      }
    } else if (path.startsWith(browserRoot) && !path.slice(browserRoot.length).includes('/')) {
      const fileName = path.slice(browserRoot.length);
      const owner = specOwner(fileName);
      if (familySet.has(owner)) {
        touchedFamilies.add(owner);
        continue;
      }
      if (fileName.endsWith('.spec.ts')) {
        harnessSpecs.add(path);
        continue;
      }
    }
    shared.push(path);
  }

  const reasons = [];
  let l4;
  if (event === 'push') {
    // Every job has work on main, whatever the diff says (a push to a new branch has no earlier commit to diff against).
    docs = true;
    code = true;
    l4 = 'full';
    reasons.push('push to main: full L4');
  } else if (labels.includes('full-l4')) {
    l4 = 'full';
    reasons.push('label full-l4: full L4');
  } else if (shared.length > 0) {
    l4 = 'full';
    reasons.push(...shared.slice(0, 5).map((path) => `shared path: ${path}`));
    if (shared.length > 5) reasons.push(`... and ${shared.length - 5} more shared paths`);
  } else if (touchedFamilies.size > 0 || harnessSpecs.size > 0) {
    l4 = 'scoped';
  } else {
    l4 = 'none';
    reasons.push(docs && !code ? 'documents only: docs-lint only' : 'no path a browser can observe');
  }

  const sortedFamilies = [...touchedFamilies].sort();
  let specs = [];
  if (l4 === 'scoped') {
    const inScope = new Set(harnessSpecs);
    for (const file of specFiles) {
      if (touchedFamilies.has(specOwner(basename(file)))) inScope.add(file);
    }
    specs = [...inScope].filter((file) => specFiles.includes(file)).sort();
    reasons.push(`families: ${sortedFamilies.join(', ') || '(none)'}`);
    if (specs.length === 0) reasons.push('no spec exists for them yet: nothing to run');
  }

  return {
    docs,
    code,
    l4,
    runL4: l4 === 'full' || (l4 === 'scoped' && specs.length > 0),
    families: sortedFamilies,
    specs,
    reasons,
  };
}

/** @param {ReturnType<typeof classify>} result @returns {string} the lines written to $GITHUB_OUTPUT */
export function formatOutputs(result) {
  return (
    [
      `docs=${result.docs}`,
      `code=${result.code}`,
      `l4=${result.l4}`,
      `run-l4=${result.runL4}`,
      `families=${result.families.join(' ')}`,
      `specs=${result.specs.join(' ')}`,
    ].join('\n') + '\n'
  );
}

function summary(result) {
  const lines = [
    '### Scoped L4',
    '',
    `| docs-lint | build-and-test | L4 | specs |`,
    `|---|---|---|---|`,
    `| ${result.docs ? 'run' : 'skip'} | ${result.code ? 'run' : 'skip'} | ${result.l4} | ${
      result.l4 === 'full' ? 'all' : result.specs.map((spec) => `\`${spec}\``).join(', ') || 'none'
    } |`,
    '',
    ...result.reasons.map((reason) => `- ${reason}`),
    '',
  ];
  return lines.join('\n');
}

function readOptions(argv) {
  const options = {};
  for (let i = 0; i < argv.length; i += 2) options[argv[i]?.replace(/^--/, '') ?? ''] = argv[i + 1] ?? '';
  return options;
}

// Three dots: what the pull request changed since it forked, not what the base branch gained meanwhile.
function gitChangedPaths(base, head) {
  return execFileSync('git', ['diff', '--name-only', `${base}...${head}`], { encoding: 'utf8' })
    .split('\n')
    .filter((line) => line.length > 0);
}

function main() {
  const options = readOptions(process.argv.slice(2));
  const event = options['event'] ?? 'pull_request';
  const labels = (options['labels'] ?? '').split(',').filter((label) => label.length > 0);
  const families = readdirSync('docs/components')
    .filter((file) => file.endsWith('.md'))
    .map((file) => file.slice(0, -'.md'.length));
  const specFiles = existsSync('tests/browser')
    ? readdirSync('tests/browser')
        .filter((file) => file.endsWith('.spec.ts'))
        .map((file) => `${browserRoot}${file}`)
    : [];
  // A push to a new branch has no usable "before" commit; the classification is then full anyway.
  const changed = event !== 'push' && options['base'] && options['head'] ? gitChangedPaths(options['base'], options['head']) : [];

  const result = classify({ changed, labels, event, families, specFiles });
  const outputs = formatOutputs(result);
  // The log carries the same text as the outputs and the summary, so a run can be read without opening the summary.
  process.stdout.write(`${outputs}${summary(result)}`);
  if (process.env['GITHUB_OUTPUT']) appendFileSync(process.env['GITHUB_OUTPUT'], outputs);
  if (process.env['GITHUB_STEP_SUMMARY']) appendFileSync(process.env['GITHUB_STEP_SUMMARY'], summary(result));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
