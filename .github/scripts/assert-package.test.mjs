// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { checkPackageEntries } from './assert-package.mjs';

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
