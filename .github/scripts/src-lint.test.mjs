// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';

// The lint over src/ (issue 34 and its comments; ADR 08 Amendments 2026-10-20): a `///` line or a string literal in a
// .cs file under src/ must not cite the specification; a boolean summary reads "Gets a value indicating whether"; and
// `///` lines use US spelling. Ordinary `//` comments, CONTEXT.md files and everything outside src/ are exempt.
// Each case plants a file in a scratch git repository and runs the script there, as CI runs it at the repository root.
const script = resolve(import.meta.dirname, 'src-lint.mjs');
const scratchRoots = [];

afterEach(() => {
  for (const root of scratchRoots.splice(0)) rmSync(root, { recursive: true, force: true });
});

/** @param {Record<string, string>} files @returns {{ status: number, output: string }} */
function lint(files) {
  const root = mkdtempSync(join(tmpdir(), 'src-lint-'));
  scratchRoots.push(root);
  execFileSync('git', ['init', '-q'], { cwd: root });
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(join(root, dirname(path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  execFileSync('git', ['add', '-A'], { cwd: root });
  try {
    const output = execFileSync(process.execPath, [script], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
    return { status: 0, output };
  } catch (error) {
    return { status: error.status ?? 1, output: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}

const cs = (...lines) => lines.join('\n') + '\n';
const doc = (text) => `    /// ${text}`;

describe('specification references', () => {
  it.each([
    ['an ADR number', 'Resolves the callback (ADR 04 decision 3).'],
    ['an ADR number with a hyphen', 'See ADR-02 for the rule.'],
    ['a requirement ID', 'Implements UTL-ALG-11 once per page.'],
    ['a section sign', 'The rule of §4.2 applies.'],
    ['a docs path', 'Described in docs/architecture/shared-utilities.md.'],
    ['a decision record', 'Settled by a decision record.'],
    ['the glossary', 'A Glossary term.'],
  ])('fails a /// line with %s', (_name, text) => {
    const result = lint({ 'src/Blazix.Primitives/Part.cs': cs('class Part', '{', doc(text), '    int X;', '}') });
    expect(result.status).toBe(1);
    expect(result.output).toContain('src/Blazix.Primitives/Part.cs:3');
  });

  it('finds an indented /// line (the macOS -E bug skipped these)', () => {
    const result = lint({ 'src/A.cs': cs('class A', '{', '        /// Per ADR 08 §5.', '    int X;', '}') });
    expect(result.status).toBe(1);
  });

  it.each([
    ['a plain string', 'throw new InvalidOperationException("Both are set (ADR 02 decision 1).");'],
    ['a requirement ID', 'var message = "RND-EDGE-03 failed";'],
    ['an interpolated string', 'var message = $"see docs/x.md for {name}";'],
    ['a string followed by a trailing comment', 'Log("§ rule"); // fine here'],
  ])('fails a string literal: %s', (_name, line) => {
    const result = lint({ 'src/A.cs': cs('class A', '{', '    void M() {', `        ${line}`, '    }', '}') });
    expect(result.status).toBe(1);
    expect(result.output).toContain('src/A.cs:4');
  });

  it('passes the same text in a // comment, after code in a trailing comment, and in CONTEXT.md', () => {
    const result = lint({
      'src/A.cs': cs(
        'class A',
        '{',
        '    // ADR 04 §3; UTL-ALG-11; docs/architecture/x.md',
        '    void M() { Log("ok"); // RND-EDGE-03 and ADR 02, "quoted" here',
        '    }',
        '}',
      ),
      'src/CONTEXT.md': 'ADR 04 §3 UTL-ALG-11 docs/architecture/x.md\n',
    });
    expect(result.status).toBe(0);
  });

  it('passes the same text outside src/', () => {
    const result = lint({ 'tests/A.cs': cs('/// ADR 04 §3', 'class A { }') });
    expect(result.status).toBe(0);
  });

  it('passes ordinary documentation', () => {
    const result = lint({
      'src/A.cs': cs('/// <summary>Gets the part\'s id, for example <c>base-ui-3</c>.</summary>', 'class A { string Id => "base-ui-3"; }'),
    });
    expect(result.status).toBe(0);
  });
});

describe('boolean summaries', () => {
  it('fails "Gets a value that indicates whether"', () => {
    const result = lint({ 'src/A.cs': cs('class A', '{', doc('<summary>Gets a value that indicates whether the part is open.</summary>'), '    bool Open;', '}') });
    expect(result.status).toBe(1);
    expect(result.output).toContain('src/A.cs:3');
  });

  it('passes "Gets a value indicating whether"', () => {
    const result = lint({ 'src/A.cs': cs('class A', '{', doc('<summary>Gets a value indicating whether the part is open.</summary>'), '    bool Open;', '}') });
    expect(result.status).toBe(0);
  });
});

describe('US spelling in /// lines', () => {
  it.each([
    'catalogue',
    'honour',
    'honours',
    'normalise',
    'normalises',
    'normalised',
    'memoise',
    'memoised',
    'serialise',
    'serialised',
    'cancelled',
    'initialise',
    'initialises',
    'initialised',
    'behaviour',
    'colour',
  ])('fails %s', (word) => {
    const result = lint({ 'src/A.cs': cs('class A', '{', doc(`<summary>It ${word} the value.</summary>`), '    int X;', '}') });
    expect(result.status).toBe(1);
    expect(result.output).toContain('src/A.cs:3');
  });

  it('is case-insensitive', () => {
    expect(lint({ 'src/A.cs': cs(doc('Behaviour of the part.')) }).status).toBe(1);
  });

  it.each(['catalog', 'honor', 'normalizes', 'memoized', 'serialized', 'canceled', 'initializes', 'behavior', 'color', 'cancel'])(
    'passes the US form %s',
    (word) => {
      expect(lint({ 'src/A.cs': cs(doc(`<summary>It ${word} the value.</summary>`)) }).status).toBe(0);
    },
  );

  it('does not check string literals or // comments for spelling', () => {
    const result = lint({ 'src/A.cs': cs('class A', '{', '    // behaviour, colour', '    string Name = "colour";', '}') });
    expect(result.status).toBe(0);
  });
});

describe('reporting', () => {
  it('lists every finding with its file and line, then exits 1', () => {
    const result = lint({
      'src/A.cs': cs('/// ADR 04 §3', 'class A { }'),
      'src/B/B.cs': cs('class B', '{', '    /// <summary>Gets a value that indicates whether it is set.</summary>', '    bool Set;', '}'),
    });
    expect(result.status).toBe(1);
    expect(result.output).toContain('src/A.cs:1');
    expect(result.output).toContain('src/B/B.cs:3');
  });

  it('exits 0 with nothing to report on an empty src/', () => {
    expect(lint({ 'README.md': 'x\n' }).status).toBe(0);
  });
});
