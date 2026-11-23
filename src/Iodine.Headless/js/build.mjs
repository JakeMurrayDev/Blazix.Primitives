#!/usr/bin/env node
// The esbuild step, run from MSBuild before the C# build (node at build time only).
//
//   node build.mjs --root <project dir> --outdir <dir relative to root> --configuration Debug|Release \
//                  [--clean <dir relative to root>] [--manifest <file>] --entry <source>=<output name> [--entry ...]
//
// --clean removes that directory first, so a Release build leaves no Debug sourcemaps behind (the library passes
// its whole wwwroot/, which is generated; the fixture client passes its wwwroot/js/). --manifest writes the list of
// emitted files (relative to root, one per line) for MSBuild to register as static web assets without globbing, and
// beside it (`<name>.inputs.json` for `<name>.manifest.txt`) each output with its inputs, which the pack check reads.
//
// One ESM entry per module into the output directory, shared code as chunks under js/chunks/, legal comments
// kept inline so the `/*! … */` banner of js/floating-ui.ts survives in js/positioning.js (the vendored engine has none of
// its own; ADR 07 Amendments 2026-10-27), sourcemaps and no minification in Debug.
import * as esbuild from 'esbuild';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';
import { resolve } from 'node:path';

const options = { root: process.cwd(), outdir: 'wwwroot', configuration: 'Debug', clean: undefined, manifest: undefined, entries: [] };
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i += 1) {
  const flag = argv[i];
  const value = argv[i + 1];
  if (value === undefined) {
    throw new Error(`Missing value for ${flag}`);
  }
  switch (flag) {
    case '--root':
      options.root = resolve(value);
      break;
    case '--outdir':
      options.outdir = value;
      break;
    case '--configuration':
      options.configuration = value;
      break;
    case '--clean':
      options.clean = value;
      break;
    case '--manifest':
      options.manifest = value;
      break;
    case '--entry': {
      const separator = value.indexOf('=');
      if (separator < 0) {
        throw new Error(`--entry expects <source>=<output name>, got '${value}'`);
      }
      options.entries.push({ in: value.slice(0, separator), out: value.slice(separator + 1) });
      break;
    }
    default:
      throw new Error(`Unknown argument ${flag}`);
  }
  i += 1;
}

if (options.entries.length === 0) {
  throw new Error('No --entry given');
}

const debug = options.configuration !== 'Release';

if (options.clean !== undefined) {
  await rm(resolve(options.root, options.clean), { recursive: true, force: true });
}

const result = await esbuild.build({
  absWorkingDir: options.root,
  entryPoints: options.entries,
  outdir: options.outdir,
  bundle: true,
  format: 'esm',
  platform: 'browser',
  target: 'es2022',
  splitting: true,
  chunkNames: 'js/chunks/[name]-[hash]',
  sourcemap: debug ? 'linked' : false,
  minify: !debug,
  legalComments: 'inline',
  logLevel: 'warning',
  metafile: true,
});

if (options.manifest !== undefined) {
  const manifest = resolve(options.root, options.manifest);
  await mkdir(dirname(manifest), { recursive: true });
  await writeFile(manifest, `${Object.keys(result.metafile.outputs).sort().join('\n')}\n`);
  // ADR 07 Amendments 2026-10-28: each output with its inputs, beside the list, so the pack check finds the output that
  // bundles the Floating UI engine wherever esbuild's splitting puts it.
  const inputs = Object.fromEntries(
    Object.entries(result.metafile.outputs)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([output, meta]) => [output, Object.keys(meta.inputs).sort()]),
  );
  await writeFile(manifest.replace(/\.manifest\.txt$|$/, '.inputs.json'), `${JSON.stringify(inputs, null, 2)}\n`);
}
