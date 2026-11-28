// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { loadDiff, mapRange, migrateCitation, migrateMarkdown, parseDiff, run } from './migrate-citations.mjs';

// ADR 15 section 2 step 2 and its verification item (issue 24): the tool that maps `Source:` citations from one Base UI
// tag to the next.

const numbered = (count, label) => Array.from({ length: count }, (_, index) => `${label} ${index + 1}`);

describe('mapping a cited range through hunks', () => {
  it('shifts a range by the net size of the hunks before it', () => {
    const hunks = [
      { oldStart: 2, oldCount: 0, newStart: 3, newCount: 3 }, // three lines inserted after old line 2
      { oldStart: 5, oldCount: 2, newStart: 9, newCount: 1 }, // old 5-6 replaced by one line
    ];
    expect(mapRange({ start: 10, end: 12 }, hunks)).toEqual({ start: 12, end: 14, changed: false });
    expect(mapRange({ start: 1, end: 2 }, hunks)).toEqual({ start: 1, end: 2, changed: false });
  });

  it('flags a range that a hunk replaces or removes lines of, or inserts lines strictly inside', () => {
    expect(mapRange({ start: 4, end: 6 }, [{ oldStart: 6, oldCount: 1, newStart: 6, newCount: 1 }]).changed).toBe(true);
    expect(mapRange({ start: 4, end: 6 }, [{ oldStart: 3, oldCount: 2, newStart: 3, newCount: 0 }]).changed).toBe(true);
    expect(mapRange({ start: 4, end: 6 }, [{ oldStart: 4, oldCount: 0, newStart: 5, newCount: 2 }]).changed).toBe(true);
    // Lines inserted right after the range's last line, or right before its first, leave it intact.
    expect(mapRange({ start: 4, end: 6 }, [{ oldStart: 6, oldCount: 0, newStart: 7, newCount: 2 }])).toEqual({ start: 4, end: 6, changed: false });
    expect(mapRange({ start: 4, end: 6 }, [{ oldStart: 3, oldCount: 0, newStart: 4, newCount: 2 }])).toEqual({ start: 6, end: 8, changed: false });
  });
});

describe('one citation', () => {
  const diff = parseDiff(
    [
      'diff --git a/packages/react/src/a/A.tsx b/packages/react/src/b/A.tsx',
      'similarity index 90%',
      'rename from packages/react/src/a/A.tsx',
      'rename to packages/react/src/b/A.tsx',
      '--- a/packages/react/src/a/A.tsx',
      '+++ b/packages/react/src/b/A.tsx',
      '@@ -1,0 +2,2 @@ header',
      '+one',
      '+two',
      '@@ -20 +22 @@',
      '-old',
      '+new',
      'diff --git a/packages/react/test/gone.tsx b/packages/react/test/gone.tsx',
      'deleted file mode 100644',
      '--- a/packages/react/test/gone.tsx',
      '+++ /dev/null',
      '@@ -1 +0,0 @@',
      '-x',
    ].join('\n'),
  );
  const old = new Set(['packages/react/src/a/A.tsx', 'packages/react/test/gone.tsx', 'packages/utils/src/u.ts']);
  const existsInOld = (path) => old.has(path);

  it('renames the path and shifts every part of a comma list, keeping its separators', () => {
    expect(migrateCitation('a/A.tsx:5,8-9, 12', diff, existsInOld)).toEqual({ kind: 'moved', text: 'b/A.tsx:7,10-11, 14' });
  });

  it('flags a citation with a changed part and proposes the mapping of the rest', () => {
    const result = migrateCitation('a/A.tsx:5,18-21', diff, existsInOld);
    expect(result.kind).toBe('flagged');
    expect(result.text).toBe('a/A.tsx:5,18-21');
    expect(result.proposed).toBe('b/A.tsx:7,20-23');
    expect(result.reason).toContain('18-21');
  });

  it('resolves test/ and packages/ paths per conventions section 3.1, reports deleted and unresolved files, skips vendor/', () => {
    expect(migrateCitation('test/gone.tsx:1', diff, existsInOld).kind).toBe('deleted');
    expect(migrateCitation('packages/utils/src/u.ts:3-4', diff, existsInOld)).toEqual({ kind: 'unchanged', text: 'packages/utils/src/u.ts:3-4' });
    expect(migrateCitation('Missing.tsx:3', diff, existsInOld).kind).toBe('unresolved');
    expect(migrateCitation('vendor/@floating-ui_dom/dist/x.mjs:3', diff, existsInOld).kind).toBe('skipped');
    expect(migrateCitation('a/A.tsx', diff, existsInOld)).toEqual({ kind: 'moved', text: 'b/A.tsx' });
    expect(migrateCitation('onValueChange', diff, existsInOld).kind).toBe('not-a-citation');
  });

  // Issue 391: the forms of conventions section 3.1 that the first version did not resolve.
  it('resolves a docs page from the docs root, CHANGELOG.md from the clone root and a reference/base-ui/ path from the clone root, skips reference/vendor/', () => {
    const pages = parseDiff(
      [
        'diff --git a/docs/src/app/(docs)/react/components/menu/page.mdx b/docs/src/app/(docs)/react/components/menu-new/page.mdx',
        'similarity index 95%',
        'rename from docs/src/app/(docs)/react/components/menu/page.mdx',
        'rename to docs/src/app/(docs)/react/components/menu-new/page.mdx',
        'diff --git a/test/setupVitest.ts b/test/setup/setupVitest.ts',
        'similarity index 95%',
        'rename from test/setupVitest.ts',
        'rename to test/setup/setupVitest.ts',
      ].join('\n'),
    );
    const tree = new Set([
      'docs/src/app/(docs)/react/components/menu/page.mdx',
      'docs/src/app/(docs)/react/handbook/customization/page.mdx',
      'CHANGELOG.md',
      'test/setupVitest.ts',
      'packages/react/package.json',
    ]);
    const inTree = (path) => tree.has(path);
    expect(migrateCitation('components/menu/page.mdx', pages, inTree)).toEqual({ kind: 'moved', text: 'components/menu-new/page.mdx' });
    expect(migrateCitation('handbook/customization/page.mdx:59-75', pages, inTree).kind).toBe('unchanged');
    expect(migrateCitation('docs/src/app/(docs)/react/handbook/customization/page.mdx:6-37', pages, inTree).kind).toBe('unchanged');
    expect(migrateCitation('CHANGELOG.md', pages, inTree).kind).toBe('unchanged');
    expect(migrateCitation('reference/base-ui/test/setupVitest.ts:30-43', pages, inTree)).toEqual({
      kind: 'moved',
      text: 'reference/base-ui/test/setup/setupVitest.ts:30-43',
    });
    expect(migrateCitation('reference/base-ui/packages/react/package.json:112-119', pages, inTree).kind).toBe('unchanged');
    expect(migrateCitation('reference/vendor/@floating-ui_utils/dist/floating-ui.utils.dom.mjs:13-16', pages, inTree).kind).toBe('skipped');
  });

  it('reads a code identifier or a port document name in backticks as no citation', () => {
    for (const span of ['Object.is', 'console.error', 'Field.Control', 'Select.Trigger', 'RadioGroup.onValueChange', 'Toast.Close']) {
      expect(migrateCitation(span, diff, existsInOld).kind).toBe('not-a-citation');
    }
    expect(migrateCitation('rendering-and-composition.md', diff, existsInOld).kind).toBe('not-a-citation');
    expect(migrateCitation('popup-store-and-handles.md', diff, existsInOld).kind).toBe('not-a-citation');
    // Base UI's own file kinds stay citations.
    expect(migrateCitation('a/A.d.ts', diff, existsInOld).kind).toBe('unresolved');
    expect(migrateCitation('a/A.css:3', diff, existsInOld).kind).toBe('unresolved');
  });

  it('rewrites only the Source part of a line, never its Test part', () => {
    const markdown = '- **X-01** — MUST. `a/A.tsx:5` in prose.\n  Source: `a/A.tsx:5`. Test: `a/A.tsx:5` "title".';
    const { text, results } = migrateMarkdown(markdown, diff, existsInOld);
    expect(text).toBe('- **X-01** — MUST. `a/A.tsx:5` in prose.\n  Source: `b/A.tsx:7`. Test: `a/A.tsx:5` "title".');
    expect(results).toEqual([{ line: 2, from: 'a/A.tsx:5', kind: 'moved', to: 'b/A.tsx:7', reason: undefined, proposed: undefined }]);
  });
});

describe('the reference clone', () => {
  let root;
  let origin;
  let clone;
  const git = (cwd, ...args) =>
    execFileSync('git', ['-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', '-c', 'init.defaultBranch=main', ...args], {
      cwd,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
  const write = (path, lines) => {
    mkdirSync(join(path, '..'), { recursive: true });
    writeFileSync(path, `${lines.join('\n')}\n`);
  };

  beforeAll(() => {
    root = mkdtempSync(join(tmpdir(), 'migrate-citations-'));
    origin = join(root, 'origin');
    clone = join(root, 'clone');
    mkdirSync(origin);
    git(origin, 'init', '-q');
    // v1: two source files of 40 lines.
    write(join(origin, 'packages/react/src/menu/MenuRoot.tsx'), numbered(40, 'menu root'));
    write(join(origin, 'packages/react/src/utils/useThing.ts'), numbered(40, 'use thing'));
    git(origin, 'add', '-A');
    git(origin, 'commit', '-q', '-m', 'v1');
    git(origin, 'tag', 'v1');
    // Commits between the tags, which the clone never fetches.
    for (let index = 0; index < 3; index++) {
      write(join(origin, `packages/react/src/noise${index}.ts`), [`${index}`]);
      git(origin, 'add', '-A');
      git(origin, 'commit', '-q', '-m', `noise ${index}`);
    }
    // v2: MenuRoot moves into a folder of its own with three lines inserted after line 10; useThing changes line 30.
    const menu = numbered(40, 'menu root');
    menu.splice(10, 0, 'added 1', 'added 2', 'added 3');
    git(origin, 'rm', '-q', 'packages/react/src/menu/MenuRoot.tsx');
    write(join(origin, 'packages/react/src/menu/root/MenuRoot.tsx'), menu);
    const thing = numbered(40, 'use thing');
    thing[29] = 'use thing 30, rewritten';
    write(join(origin, 'packages/react/src/utils/useThing.ts'), thing);
    git(origin, 'add', '-A');
    git(origin, 'commit', '-q', '-m', 'v2');
    git(origin, 'tag', 'v2');
    // The reference checkout's shape (docs/conventions.md section 1): a depth-1 clone of the pinned tag.
    git(root, 'clone', '-q', '--depth', '1', '--branch', 'v1', pathToFileURL(origin).href, clone);
  });

  afterAll(() => rmSync(root, { recursive: true, force: true }));

  it('names the fetch to run when the new tag is not in the clone', () => {
    expect(() => loadDiff(clone, 'v1', 'v2')).toThrow(`git -C ${clone} fetch --depth 1 origin tag v2`);
  });

  it('@verifies ADR-15/1 maps citations through a renamed file on a shallow clone that holds only the two tags', () => {
    git(clone, 'fetch', '-q', '--depth', '1', 'origin', 'tag', 'v2');
    expect(git(clone, 'rev-parse', '--is-shallow-repository').trim()).toBe('true');
    expect(git(clone, 'rev-list', '--count', 'v2').trim()).toBe('1'); // no history between the tags

    const doc = join(root, 'menu.md');
    writeFileSync(
      doc,
      [
        '- **MNU-01** — MUST. Opens.',
        '  Source: `menu/MenuRoot.tsx:5-8,12`, `utils/useThing.ts:2`. Test: [L2] `MenuRoot.test.tsx` "opens".',
        '- **MNU-02** — MUST. Closes.',
        '  Source: `menu/MenuRoot.tsx:9-11`, `utils/useThing.ts:28-31`. Test: none.',
        '',
      ].join('\n'),
    );
    const { report, counts } = run({ repo: clone, from: 'v1', to: 'v2', files: [doc], write: true });

    expect(counts).toEqual({ unchanged: 1, moved: 1, flagged: 2, deleted: 0, unresolved: 0, skipped: 0 });
    expect(readFileSync(doc, 'utf8').split('\n').slice(1, 4)).toEqual([
      '  Source: `menu/root/MenuRoot.tsx:5-8,15`, `utils/useThing.ts:2`. Test: [L2] `MenuRoot.test.tsx` "opens".',
      '- **MNU-02** — MUST. Closes.',
      '  Source: `menu/MenuRoot.tsx:9-11`, `utils/useThing.ts:28-31`. Test: none.',
    ]);
    expect(report).toContain('**flagged** `menu/MenuRoot.tsx:9-11`: changed inside 9-11 (proposed `menu/root/MenuRoot.tsx:9-14`)');
    expect(report).toContain('**flagged** `utils/useThing.ts:28-31`');
  });
});
