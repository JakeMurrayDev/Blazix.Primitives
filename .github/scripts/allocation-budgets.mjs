// The allocation budgets, one test class per process (ADR 16 Amendments 2026-11-11, issue 216; ADR 11 Amendments
// 2026-11-11). A budget's figure is the least of five runs of its operation in one process. Inside a full run of its
// project, the state the earlier tests leave in the process moves that floor by 80 to 192 bytes in some processes, while a
// class run alone measures its floor every time. So the main `dotnet test` runs skip the classes of the `AllocationBudgets`
// collection, and this script runs each of them alone. Run from the repository root, after a Release build:
//
//   node .github/scripts/allocation-budgets.mjs
//       every budget class of the two projects that hold budgets, one `dotnet test` process each
//   node .github/scripts/allocation-budgets.mjs --exclusions tests/Blazix.Primitives.Tests
//       prints the `--filter-not-class` arguments a main run of that project passes (nothing for a project without budgets)
//
// A class belongs to the collection when its declaration carries `[Collection(AllocationBudgetsCollection.Name)]` or
// `[Collection("AllocationBudgets")]`, or, for a Razor test component, when its `@attribute` directive does; the component's
// class is named by its `@namespace` directive and its file name. Options: `--configuration <name>` (Release by default). A class whose filter runs no
// test fails, so a class this parser names wrongly cannot pass unnoticed.
import { spawnSync } from 'node:child_process';
import { appendFileSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { pathToFileURL } from 'node:url';

/** The test projects that hold allocation budgets. */
export const budgetProjects = ['tests/Blazix.Primitives.Tests', 'tests/Blazix.Primitives.Tests.Components'];

const collectionAttribute = /\[\s*Collection\s*\(\s*(?:AllocationBudgetsCollection\s*\.\s*Name|"AllocationBudgets")\s*\)\s*\]/g;
const namespaceDeclaration = /^\s*namespace\s+([\w.]+)\s*[;{]/m;
const classDeclaration = /\bclass\s+(\w+)/g;
const razorNamespace = /^\s*@namespace\s+([\w.]+)/m;

/**
 * The fully qualified names of the classes a source file declares in the `AllocationBudgets` collection: the classes a C#
 * file declares under the attribute, or the Razor test component a `.razor` file is, when its `@attribute` directive
 * carries the attribute.
 * @param {string} source
 * @param {string} [path] the file's path; a `.razor` path reads the source as a Razor component
 * @returns {string[]}
 */
export function budgetClasses(source, path = '') {
  if (path.endsWith('.razor')) {
    const directive = new RegExp(`^\\s*@attribute\\s+${collectionAttribute.source}`, 'm');
    if (!directive.test(source)) return [];
    const name = basename(path, '.razor');
    const namespace = razorNamespace.exec(source)?.[1];
    return [namespace ? `${namespace}.${name}` : name];
  }

  const namespace = namespaceDeclaration.exec(source)?.[1];
  const names = [];
  for (const attribute of source.matchAll(collectionAttribute)) {
    classDeclaration.lastIndex = attribute.index + attribute[0].length;
    const declared = classDeclaration.exec(source);
    if (declared) names.push(namespace ? `${namespace}.${declared[1]}` : declared[1]);
  }
  return names;
}

/**
 * The arguments that make a main run of a project skip its budget classes.
 * @param {string[]} classes
 * @returns {string[]}
 */
export function exclusionArgs(classes) {
  return classes.flatMap((name) => ['--filter-not-class', name]);
}

/**
 * The step summary of a budget run, and whether it passed: every class passed and ran at least one test.
 * @param {{ project: string, name: string, status: number, seconds: number }[]} results
 * @returns {{ ok: boolean, markdown: string }}
 */
export function summarize(results) {
  // Microsoft.Testing.Platform exits 0 on success, 2 on a failed test and 8 when the filter ran no test.
  const outcome = (status) => (status === 0 ? 'passed' : status === 8 ? 'ran no test' : 'failed');
  const failed = results.filter((result) => result.status !== 0);
  const seconds = results.reduce((sum, result) => sum + result.seconds, 0);
  const rows = results.map((result) => `| ${result.project} | \`${result.name}\` | ${outcome(result.status)} | ${result.seconds.toFixed(1)} |`);
  const verdict = results.length === 0
    ? 'No budget class was found.'
    : failed.length === 0
      ? `All ${results.length} budget classes passed, one process each (${seconds.toFixed(0)} s).`
      : `${failed.length} of ${results.length} budget classes did not pass.`;
  const markdown = [
    '### Allocation budgets, one class per process',
    '',
    verdict,
    '',
    '| Project | Class | Outcome | Seconds |',
    '|---|---|---|---|',
    ...rows,
    '',
  ].join('\n');
  return { ok: results.length > 0 && failed.length === 0, markdown };
}

/** @param {string} directory @returns {string[]} */
function sourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return entry.name === 'bin' || entry.name === 'obj' ? [] : sourceFiles(path);
    return entry.name.endsWith('.cs') || entry.name.endsWith('.razor') ? [path] : [];
  });
}

/** @param {string} project @returns {string[]} */
function projectBudgetClasses(project) {
  return sourceFiles(project).flatMap((path) => budgetClasses(readFileSync(path, 'utf8'), path)).sort();
}

function main() {
  const args = process.argv.slice(2);
  const option = (name) => {
    const index = args.indexOf(`--${name}`);
    return index >= 0 ? args[index + 1] : undefined;
  };

  const excluded = option('exclusions');
  if (excluded !== undefined) {
    const classes = budgetProjects.includes(excluded.replace(/\/+$/, '')) ? projectBudgetClasses(excluded) : [];
    process.stdout.write(exclusionArgs(classes).join(' '));
    return;
  }

  const configuration = option('configuration') ?? 'Release';
  const results = [];
  for (const project of budgetProjects) {
    for (const name of projectBudgetClasses(project)) {
      const started = Date.now();
      const run = spawnSync('dotnet', ['test', '--project', project, '-c', configuration, '--no-build', '--filter-class', name], { encoding: 'utf8' });
      const result = { project, name, status: run.status ?? 1, seconds: (Date.now() - started) / 1000 };
      results.push(result);
      console.log(`${result.status === 0 ? 'passed' : 'FAILED'}  ${name}  (${result.seconds.toFixed(1)} s)`);
      if (result.status !== 0) process.stdout.write(`${run.stdout ?? ''}${run.stderr ?? ''}\n`);
    }
  }

  const { ok, markdown } = summarize(results);
  console.log(`\n${markdown}`);
  if (process.env['GITHUB_STEP_SUMMARY']) appendFileSync(process.env['GITHUB_STEP_SUMMARY'], `${markdown}\n`);
  process.exit(ok ? 0 : 1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
