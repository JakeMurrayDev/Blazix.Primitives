// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { checkEngineBanner, checkPackageEntries, engineOutput, engineVersions } from './assert-package.mjs';

// ADR 10 decision 1 and ADR 07 decision 4: what the one NuGet package contains. The entry names are those of the
// package `dotnet pack` produces (listed with `unzip -Z1`).
const complete = [
  '_rels/.rels',
  'Blazix.Primitives.nuspec',
  'LICENSE',
  'THIRD-PARTY-NOTICES.md',
  'analyzers/dotnet/cs/Blazix.Primitives.Generators.dll',
  'build/Blazix.Primitives.targets',
  'lib/net11.0/Blazix.Primitives.dll',
  'lib/net11.0/Blazix.Primitives.xml',
  'staticwebassets/Blazix.Primitives.5055uwdyua.lib.module.js',
  'staticwebassets/js/dom.js',
  'staticwebassets/js/positioning.js',
  '[Content_Types].xml',
];

describe('package content', () => {
  it('accepts a package with the licence files, the generator, the library and the JavaScript', () => {
    expect(checkPackageEntries(complete)).toEqual([]);
  });

  it.each([
    ['LICENSE', 'LICENSE'],
    ['THIRD-PARTY-NOTICES.md', 'THIRD-PARTY-NOTICES.md'],
    ['analyzers/dotnet/cs/Blazix.Primitives.Generators.dll', 'the generator under analyzers/dotnet/cs'],
    ['lib/net11.0/Blazix.Primitives.dll', 'the library assembly'],
    ['staticwebassets/Blazix.Primitives.5055uwdyua.lib.module.js', 'the JavaScript initializer under staticwebassets'],
    ['staticwebassets/js/positioning.js', 'staticwebassets/js/positioning.js'],
  ])('reports a package without %s', (entry, expected) => {
    const problems = checkPackageEntries(complete.filter((name) => name !== entry));
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain(expected);
  });

  it('reports a package whose staticwebassets hold no JavaScript at all (the JavaScript build did not run)', () => {
    const problems = checkPackageEntries(complete.filter((name) => !name.startsWith('staticwebassets/')));
    expect(problems.join('\n')).toContain('staticwebassets');
  });

  it('reports the internal analyzers assembly, which is never packed', () => {
    const problems = checkPackageEntries([...complete, 'analyzers/dotnet/cs/Blazix.Primitives.Analyzers.dll']);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('Blazix.Primitives.Analyzers.dll');
  });
});

// ADR 07 Amendments 2026-10-27 (issue 16): the vendored Floating UI dist files carry no legal comment, so the notice
// ships as the `/*! … */` comment of js/floating-ui.ts, which esbuild keeps inline in js/positioning.js (the engine's only
// importer, pinned by the module-graph lint). Minification renames `computePosition`, so the output is found by name.
const versions = { core: '1.8.0', dom: '1.8.0', utils: '0.2.12' };
const banner =
  '/*! Floating UI: @floating-ui/core 1.8.0, @floating-ui/dom 1.8.0, @floating-ui/utils 0.2.12 | MIT License | Copyright (c) 2021-present Floating UI contributors | https://floating-ui.com | Full notice: THIRD-PARTY-NOTICES.md */';
const minifiedEngine = 'function Ae(t,e,n){return t.x+e}const Ie={clippingAncestors:1};export{Ae as computePosition};';

describe('the Floating UI banner in the packed js/positioning.js', () => {
  it('@verifies ADR-07/1 accepts the minified engine output that carries the wrapper banner inline, and reports one that lacks it', () => {
    expect(checkEngineBanner(`${minifiedEngine.slice(0, 40)}${banner}\n${minifiedEngine.slice(40)}`, versions)).toEqual([]);
    const problems = checkEngineBanner(minifiedEngine, versions);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('staticwebassets/js/positioning.js');
    expect(problems[0]).toContain('Floating UI');
  });

  it('reports a legal comment that does not name Floating UI', () => {
    const problems = checkEngineBanner(`/*! some other notice, MIT */${minifiedEngine}`, versions);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('no /*! … */ legal comment naming Floating UI');
  });

  it.each([
    ['@floating-ui/core 1.8.0', banner.replace('@floating-ui/core 1.8.0', '@floating-ui/core 1.7.0')],
    ['@floating-ui/dom 1.8.0', banner.replace('@floating-ui/dom 1.8.0', '@floating-ui/dom')],
    ['@floating-ui/utils 0.2.12', banner.replace('@floating-ui/utils 0.2.12', '@floating-ui/utils')],
    ['MIT', banner.replace('MIT License', 'licence')],
    ['Copyright (c) 2021-present Floating UI contributors', banner.replace('Copyright (c) 2021-present Floating UI contributors', '')],
  ])('reports a banner that does not name %s', (expected, stale) => {
    const problems = checkEngineBanner(`${stale}${minifiedEngine}`, versions);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain(expected);
  });

  it('reads the pinned versions from package.json (the dom dependency and the core and utils overrides)', () => {
    expect(engineVersions({ devDependencies: { '@floating-ui/dom': '1.9.1' }, overrides: { '@floating-ui/core': '1.9.0', '@floating-ui/utils': '0.3.0' } })).toEqual({
      core: '1.9.0',
      dom: '1.9.1',
      utils: '0.3.0',
    });
  });
});

// ADR 07 Amendments 2026-10-28 (settled by the orchestrator; M4 track 5): once a second entry imports positioning (the
// popup module), esbuild moves the engine and its legal comment into a shared chunk and js/positioning.js becomes a
// re-export, so the check finds the output whose inputs include js/floating-ui.ts in the build's inputs manifest.
describe('the packed output that bundles the engine', () => {
  const inputs = {
    'wwwroot/js/positioning.js': ['js/positioning.ts'],
    'wwwroot/js/popup.js': ['js/popup.ts'],
    'wwwroot/js/chunks/chunk-MRCTZCWJ.js': ['js/floating-ui.ts', 'js/positioning.ts', '../../node_modules/@floating-ui/core/dist/floating-ui.core.mjs'],
  };

  it('is the chunk whose inputs include the wrapper, as its packed static web asset', () => {
    expect(engineOutput(inputs)).toBe('staticwebassets/js/chunks/chunk-MRCTZCWJ.js');
  });

  it('is js/positioning.js while positioning is the only importer, and nothing without the wrapper', () => {
    expect(engineOutput({ 'wwwroot/js/positioning.js': ['js/floating-ui.ts', 'js/positioning.ts'] })).toBe('staticwebassets/js/positioning.js');
    expect(engineOutput({ 'wwwroot/js/dom.js': ['js/dom.ts'] })).toBe(null);
  });

  it('names the chunk in a missing-banner report', () => {
    const problems = checkEngineBanner(minifiedEngine, versions, 'staticwebassets/js/chunks/chunk-MRCTZCWJ.js');
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain('staticwebassets/js/chunks/chunk-MRCTZCWJ.js');
  });
});
