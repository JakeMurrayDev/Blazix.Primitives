#!/usr/bin/env node
// The esbuild step of ADR 04 §4, run from MSBuild before the C# build (node at build time only).
//
//   node build.mjs --root <project dir> --outdir <dir relative to root> --configuration Debug|Release \
//                  [--clean <dir relative to root>] [--manifest <file>] --entry <source>=<output name> [--entry ...]
//
// --clean removes that directory first, so a Release build leaves no Debug sourcemaps behind (the library passes
// its whole wwwroot/, which is generated; the fixture client passes its wwwroot/js/). --manifest writes the list of
// emitted files (relative to root, one per line) for MSBuild to register as static web assets without globbing.
//
// One ESM entry per module into the output directory, shared code as chunks under js/chunks/, legal comments
// kept inline so the Floating UI banner survives (ADR 07), sourcemaps and no minification in Debug.
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
}
