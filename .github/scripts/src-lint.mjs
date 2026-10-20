// The lint over src/ (issue 34 and its comments; ADR 08 Amendments 2026-10-20). Run from the repository root:
//
//   node .github/scripts/src-lint.mjs
//
// It fails (exit 1, one `path:line:text` per finding) when a .cs file under src/ has
//   1. a `///` line or a string literal that cites the specification (ADR numbers, requirement IDs, section signs,
//      docs/ paths, "decision record", "glossary"): the shipped surface carries no reference to the documents;
//   2. a `///` line written "a value that indicates whether" (the form is "a value indicating whether");
//   3. a `///` line with a UK spelling (catalogue, honour, normalise, memoise, serialise, initialise, cancelled,
//      behaviour, colour and their inflections).
// Ordinary `//` comments and CONTEXT.md files are exempt: that is where the citations belong.
//
// The checks use `git grep -P` on purpose. With -E, macOS's regex library ignores \s and \b, so the pattern silently
// skips indented `///` lines and every requirement ID; -P behaves the same on macOS and Ubuntu. String literals are
// recognised for regular, interpolated and verbatim strings; raw string literals (""") are not used under src/.
import { spawnSync } from 'node:child_process';

const specification = String.raw`ADR[ -]?[0-9]|\b[A-Z]{3}-(?:STR|API|STA|DOM|ARIA|KEY|PTR|FOC|DIS|EVT|FRM|ANI|POS|A11Y|I18N|EDGE|ALG)-[0-9]+|§|docs/|(?i:decision record|glossary)`;

// A line that is not a comment, a prefix of code and complete strings, then a string whose text holds the pattern.
// The possessive runs keep PCRE's JIT stack small on long lines; the outer repeat may backtrack to leave the target string.
const inStringLiteral = String.raw`^(?!\s*//)(?:[^"/]++|/(?!/)|"(?:[^"\\]++|\\.)*+")*"(?:(?!(?:${specification}))(?:[^"\\]|\\.))*+(?:${specification})`;

const checks = [
  {
    name: 'specification reference in a /// line',
    pattern: String.raw`^\s*///.*(?:${specification})`,
  },
  {
    name: 'specification reference in a string literal',
    pattern: inStringLiteral,
  },
  {
    name: 'boolean summary: write "a value indicating whether"',
    pattern: String.raw`^\s*///.*a value that indicates whether`,
  },
  {
    name: 'UK spelling in a /// line (use the US form)',
    pattern: String.raw`(?i)^\s*///.*\b(?:catalogue\w*|honour\w*|behaviour\w*|colour\w*|(?:normalis|memois|serialis|initialis)(?:e|es|ed|ing|ation|ations)|cancel(?:led|ling))\b`,
  },
];

let findings = 0;
for (const { name, pattern } of checks) {
  // --untracked: a scratch file planted before `git add` is found too. -I skips binary files.
  const result = spawnSync('git', ['grep', '--untracked', '-nIP', '-e', pattern, '--', 'src/*.cs'], { encoding: 'utf8' });
  if (result.status !== 0 && result.status !== 1) {
    process.stderr.write(`git grep failed (${result.status}): ${result.stderr}\n`);
    process.exit(2);
  }
  const lines = result.stdout.split('\n').filter((line) => line.length > 0);
  for (const line of lines) {
    const [path, lineNumber] = line.split(':', 2);
    console.log(`${line}\n    ^ ${name}`);
    if (process.env['GITHUB_ACTIONS']) console.log(`::error file=${path},line=${lineNumber}::${name}`);
  }
  findings += lines.length;
}

if (findings > 0) {
  console.error(`${findings} finding(s). Specification references belong in // comments and CONTEXT.md files; /// text uses US spelling and "indicating whether".`);
  process.exit(1);
}
console.log('src lint: clean');
