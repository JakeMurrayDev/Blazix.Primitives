// ADR 10 decision 1 and ADR 07 decision 4: asserts what the one NuGet package contains.
//
//   node .github/scripts/assert-package.mjs path/to/Blazix.Primitives.<version>.nupkg
//
// The package must hold the licence files, the library, the generator as an analyzer asset, and the built JavaScript
// as static web assets; the internal analyzers assembly must not be in it.
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

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

function main() {
  const path = process.argv[2];
  if (!path) throw new Error('usage: assert-package.mjs <package.nupkg>');
  const entries = execFileSync('unzip', ['-Z1', path], { encoding: 'utf8' }).split('\n').filter(Boolean);
  const problems = checkPackageEntries(entries);
  if (problems.length > 0) {
    console.error(`${path}:\n${problems.map((problem) => `  ${problem}`).join('\n')}`);
    process.exit(1);
  }
  console.log(`${path}: ${entries.length} entries, content as ADR 10 decision 1 requires`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main();
