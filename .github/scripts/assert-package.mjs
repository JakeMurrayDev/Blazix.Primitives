// ADR 10 decision 1 and ADR 07 decision 4: asserts what the one NuGet package contains.
//
//   node .github/scripts/assert-package.mjs path/to/Blazix.Primitives.<version>.nupkg
//
// The package must hold the licence files, the library, the generator as an analyzer asset, and the built JavaScript
// as static web assets; the internal analyzers assembly must not be in it. The packed output that bundles the Floating UI
// engine must carry the engine's legal comment (ADR 07 Amendments 2026-10-27, issue 16): the vendored dist files have none,
// so the notice is the `/*! … */` comment of js/floating-ui.ts, the wrapper only positioning imports. That output is
// js/positioning.js while positioning is the engine's only importer; once a second entry imports positioning, esbuild moves
// the engine into a shared chunk, which the build's inputs manifest names (`--inputs`, ADR 07 Amendments 2026-10-28). The
// output is found by its inputs, not its text, because minification renames `computePosition`.
//
//   node .github/scripts/assert-package.mjs path/to/Blazix.Primitives.<version>.nupkg [--inputs blazix-js.inputs.json]
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

/** The packed positioning entry, which bundles the Floating UI engine while it is the engine's only importer. */
const engineEntry = 'staticwebassets/js/positioning.js';

/** The engine wrapper, the input that marks the output that bundles the engine. */
const engineWrapper = 'js/floating-ui.ts';

/**
 * Finds the packed output that bundles the Floating UI engine.
 * @param {Record<string, string[]>} inputsByOutput the build's inputs manifest: each output under wwwroot/ with its inputs
 * @returns {string | null} the output's packed static web asset name, or `null` when no output bundles the wrapper
 */
export function engineOutput(inputsByOutput) {
  const output = Object.entries(inputsByOutput).find(([, inputs]) => inputs.includes(engineWrapper))?.[0];
  return output === undefined ? null : `staticwebassets/${output.replace(/^wwwroot\//, '')}`;
}

const required = [
  { description: 'LICENSE', test: (entry) => entry === 'LICENSE' },
  { description: 'THIRD-PARTY-NOTICES.md', test: (entry) => entry === 'THIRD-PARTY-NOTICES.md' },
  { description: 'the library assembly lib/net11.0/Blazix.Primitives.dll', test: (entry) => entry === 'lib/net11.0/Blazix.Primitives.dll' },
  {
    description: 'the generator under analyzers/dotnet/cs',
    test: (entry) => entry === 'analyzers/dotnet/cs/Blazix.Primitives.Generators.dll',
  },
  {
    description: 'the JavaScript initializer under staticwebassets (the JavaScript build did not run, or its output was not packed)',
    test: (entry) => /^staticwebassets\/.*\.lib\.module\.js$/.test(entry),
  },
  { description: `${engineEntry} (the positioning module, which bundles the Floating UI engine)`, test: (entry) => entry === engineEntry },
];

/** @param {string[]} entries the entry names of the package @returns {string[]} one message per problem */
export function checkPackageEntries(entries) {
  const problems = required.filter(({ test }) => !entries.some(test)).map(({ description }) => `missing: ${description}`);
  for (const entry of entries) {
    if (entry === 'analyzers/dotnet/cs/Blazix.Primitives.Analyzers.dll') {
      problems.push(`unexpected: ${entry} (the analyzers project is never packed)`);
    }
  }
  return problems;
}

/**
 * @param {{ devDependencies?: Record<string, string>, overrides?: Record<string, string> }} manifest the repository's package.json
 * @returns {{ core: string, dom: string, utils: string }} the pinned Floating UI versions (ADR 03, ADR 15)
 */
export function engineVersions(manifest) {
  return {
    core: manifest.overrides?.['@floating-ui/core'] ?? '',
    dom: manifest.devDependencies?.['@floating-ui/dom'] ?? '',
    utils: manifest.overrides?.['@floating-ui/utils'] ?? '',
  };
}

/**
 * @param {string} source the packed output that bundles the engine
 * @param {{ core: string, dom: string, utils: string }} versions the pinned versions the banner must name
 * @param {string} [entry] the output's packed name, for the messages; js/positioning.js by default
 * @returns {string[]} one message per problem
 */
export function checkEngineBanner(source, versions, entry = engineEntry) {
  const banner = [...source.matchAll(/\/\*![\s\S]*?\*\//g)].map((match) => match[0]).find((comment) => comment.includes('Floating UI'));
  if (banner === undefined) {
    return [`${entry}: no /*! … */ legal comment naming Floating UI, so the bundled engine ships without its MIT notice (js/floating-ui.ts carries it)`];
  }
  const names = [
    `@floating-ui/core ${versions.core}`,
    `@floating-ui/dom ${versions.dom}`,
    `@floating-ui/utils ${versions.utils}`,
    'MIT',
    'Copyright (c) 2021-present Floating UI contributors',
  ];
  return names.filter((name) => !banner.includes(name)).map((name) => `${entry}: the Floating UI legal comment does not name ${name}`);
}

function main() {
  const path = process.argv[2];
  if (!path) throw new Error('usage: assert-package.mjs <package.nupkg> [--inputs <blazix-js.inputs.json>]');
  const inputsFlag = process.argv.indexOf('--inputs');
  const inputs = inputsFlag > 0 ? JSON.parse(readFileSync(process.argv[inputsFlag + 1], 'utf8')) : null;
  const engine = inputs === null ? engineEntry : (engineOutput(inputs) ?? engineEntry);
  const entries = execFileSync('unzip', ['-Z1', path], { encoding: 'utf8' }).split('\n').filter(Boolean);
  const problems = checkPackageEntries(entries);
  if (!entries.includes(engine)) {
    problems.push(`missing: ${engine} (the output that bundles the Floating UI engine)`);
  } else {
    const source = execFileSync('unzip', ['-p', path, engine], { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 });
    const manifest = JSON.parse(readFileSync(new URL('../../package.json', import.meta.url), 'utf8'));
    problems.push(...checkEngineBanner(source, engineVersions(manifest), engine));
  }
  if (problems.length > 0) {
    console.error(`${path}:\n${problems.map((problem) => `  ${problem}`).join('\n')}`);
    process.exit(1);
  }
  console.log(`${path}: ${entries.length} entries, content as ADR 10 decision 1 requires; ${engine} carries the Floating UI banner`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
