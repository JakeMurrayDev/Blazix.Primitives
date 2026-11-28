// ADR 15 section 2 step 2: the citation-migration tool. When the Base UI pin moves, it maps every `Source:` citation
// (`path:lines` in backticks, docs/conventions.md section 3.1) from the old tag to the new one through the hunks of the
// rename-aware diff between the two tags, and flags the citations whose ranges fall inside changed code.
//
//   node docs/_tools/migrate-citations.mjs --from v1.9.0 --to v1.10.0 [--repo reference/base-ui] [--write] [files...]
//
// files default to every Markdown file under docs/. Without --write it prints a report and changes nothing; with
// --write it rewrites the citations that moved cleanly (path renamed, lines shifted) and leaves the flagged ones as
// they are, for review under ADR 15 section 2 step 3.
//
// The tool needs only the two tagged commits, not the history between them: a tree-to-tree diff (`git diff -M`) finds
// renames by content similarity, and git records no renames in history. A shallow clone works once both tags are
// fetched (`git fetch --depth 1 origin tag <tag>`); ADR 15's verification item, issue 24, and the dated note in ADR 15.
//
// A citation is one backticked span on a `Source:` line (before its `Test:` part) whose text is a source path, with or
// without line numbers, ending in one of Base UI's file kinds (`sourceExtensions`); a code identifier such as
// `Object.is` or `Select.Trigger` is no citation. Paths resolve per conventions section 3.1: `packages/` and `docs/`
// from the clone's root, `test/` under `packages/react/`, a docs page (`components/menu/page.mdx`) under the official
// docs root `docs/src/app/(docs)/react/`, `CHANGELOG.md` from the clone's root, `reference/base-ui/<path>` from the
// clone's root, anything else under `packages/react/src/`. Any other `.md` name is a cross-reference to a port document,
// not a citation. A path the old tree lacks is reported as unresolved and never changed; `vendor/` and
// `reference/vendor/` paths are outside the clone and are reported as skipped (issue 391 added the docs-page,
// CHANGELOG, `reference/` and identifier rules).
//
// Run with `--from` and `--to` both at the pin to check that every citation resolves: the diff is empty, so any
// unresolved citation is a wrong path, and the exit code is 1.
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const citationPattern = /^([\w@()[\].-]+(?:\/[\w@()[\].-]+)*\.[A-Za-z]+)(?::(\d+(?:-\d+)?(?:,\s?\d+(?:-\d+)?)*))?$/;

// The file kinds a citation may name; anything else after the last dot is a member access in code.
const sourceExtensions = new Set(['ts', 'tsx', 'mts', 'js', 'jsx', 'mjs', 'cjs', 'json', 'css', 'md', 'mdx']);

// The official docs pages (docs/conventions.md section 1).
const docsRoot = 'docs/src/app/(docs)/react/';

/**
 * Resolves a cited path to its path in the Base UI clone (conventions section 3.1), or null for a path outside it.
 * @param {string} cited
 * @returns {string | null}
 */
export function resolveCitedPath(cited) {
  if (cited.startsWith('vendor/') || cited.startsWith('reference/vendor/')) return null;
  if (cited.startsWith('reference/base-ui/')) return cited.slice('reference/base-ui/'.length);
  if (cited.startsWith('packages/') || cited.startsWith('docs/')) return cited;
  if (cited.startsWith('test/')) return `packages/react/${cited}`;
  if (cited.endsWith('.mdx')) return `${docsRoot}${cited}`;
  if (/^CHANGELOG(\.old)?\.md$/.test(cited)) return cited;
  return `packages/react/src/${cited}`;
}

/**
 * Whether a backticked span that matches the path pattern names a file: it ends in a file kind of Base UI's, and a
 * `.md` name is Base UI's (under `packages/` or `docs/`, a CHANGELOG, or behind `reference/base-ui/`) rather than a
 * port document.
 * @param {string} cited
 * @returns {boolean}
 */
export function isSourcePath(cited) {
  const extension = cited.slice(cited.lastIndexOf('.') + 1);
  if (!sourceExtensions.has(extension)) return false;
  if (extension !== 'md') return true;
  return /^(packages\/|docs\/|reference\/base-ui\/|CHANGELOG(\.old)?\.md$)/.test(cited);
}

/**
 * The inverse of resolveCitedPath for a path in the new tree, keeping the citation's short form.
 * @param {string} resolved
 * @returns {string}
 */
export function citePath(resolved) {
  if (resolved.startsWith('packages/react/src/')) return resolved.slice('packages/react/src/'.length);
  if (resolved.startsWith('packages/react/test/')) return resolved.slice('packages/react/'.length);
  return resolved;
}

/**
 * Cites a path of the new tree in the same form as the old citation: behind `reference/base-ui/` when it was, or as a
 * docs page relative to the docs root when it was one and still is.
 * @param {string} cited the old citation's path
 * @param {string} resolved the new tree's path
 * @returns {string}
 */
function citeLike(cited, resolved) {
  if (cited.startsWith('reference/base-ui/')) return `reference/base-ui/${resolved}`;
  if (!cited.startsWith('docs/') && cited.endsWith('.mdx') && resolved.startsWith(docsRoot) && resolved.endsWith('.mdx')) {
    return resolved.slice(docsRoot.length);
  }
  return citePath(resolved);
}

/**
 * Parses the output of `git diff -M --unified=0 --no-color` into a map from each changed old path to its fate.
 * @param {string} diffText
 * @returns {Map<string, { newPath: string | null, binary: boolean, hunks: { oldStart: number, oldCount: number, newStart: number, newCount: number }[] }>}
 */
export function parseDiff(diffText) {
  const files = new Map();
  let current = null;
  const finish = () => {
    // A file the old tree lacks (added) has no citations to move.
    if (current !== null && !current.added && current.oldPath !== null) {
      files.set(current.oldPath, { newPath: current.newPath, binary: current.binary, hunks: current.hunks });
    }
  };
  for (const line of diffText.split('\n')) {
    if (line.startsWith('diff --git ')) {
      finish();
      // The header's paths are the fallback (a binary change has no ---/+++ lines); the lines below refine them.
      const match = /^diff --git a\/(.+) b\/(.+)$/.exec(line);
      current = { oldPath: match?.[1] ?? null, newPath: match?.[2] ?? null, added: false, binary: false, hunks: [] };
    } else if (current === null) {
      continue;
    } else if (line.startsWith('rename from ')) {
      current.oldPath = line.slice('rename from '.length);
    } else if (line.startsWith('rename to ')) {
      current.newPath = line.slice('rename to '.length);
    } else if (line.startsWith('new file mode')) {
      current.added = true;
    } else if (line.startsWith('deleted file mode')) {
      current.newPath = null;
    } else if (line.startsWith('--- a/') && current.hunks.length === 0) {
      current.oldPath = line.slice('--- a/'.length);
    } else if (line.startsWith('+++ b/') && current.hunks.length === 0) {
      current.newPath = line.slice('+++ b/'.length);
    } else if (line.startsWith('Binary files ')) {
      current.binary = true;
    } else if (line.startsWith('@@ ')) {
      const match = /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/.exec(line);
      if (match) {
        current.hunks.push({
          oldStart: Number(match[1]),
          oldCount: match[2] === undefined ? 1 : Number(match[2]),
          newStart: Number(match[3]),
          newCount: match[4] === undefined ? 1 : Number(match[4]),
        });
      }
    }
  }
  finish();
  return files;
}

/**
 * Maps one cited range of an old file through its hunks. A range is changed when a removed or replaced old line lies
 * in it, or when lines were inserted strictly inside it; otherwise it shifts by the net size of the hunks before it.
 * The start shifts by the hunks wholly before it and the end by the hunks wholly before or inside the range, so the
 * mapping of a changed range, proposed for review, spans what replaced it.
 * @param {{ start: number, end: number }} range
 * @param {{ oldStart: number, oldCount: number, newStart: number, newCount: number }[]} hunks
 * @returns {{ start: number, end: number, changed: boolean }}
 */
export function mapRange(range, hunks) {
  let startShift = 0;
  let endShift = 0;
  let changed = false;
  for (const hunk of hunks) {
    const net = hunk.newCount - hunk.oldCount;
    if (hunk.oldCount === 0) {
      // A pure insertion after old line oldStart.
      if (hunk.oldStart < range.start) startShift += net;
      else if (hunk.oldStart < range.end) changed = true;
      if (hunk.oldStart < range.end) endShift += net;
      continue;
    }
    const first = hunk.oldStart;
    const last = hunk.oldStart + hunk.oldCount - 1;
    if (last < range.start) startShift += net;
    else if (first <= range.end) changed = true;
    if (last <= range.end) endShift += net;
  }
  return { start: range.start + startShift, end: range.end + endShift, changed };
}

/**
 * Migrates one citation's text.
 * @param {string} text the span inside the backticks
 * @param {Map<string, object>} diff from parseDiff
 * @param {(resolved: string) => boolean} existsInOld whether the old tree has the path
 * @returns {{ kind: 'unchanged' | 'moved' | 'flagged' | 'deleted' | 'unresolved' | 'skipped' | 'not-a-citation', text: string, reason?: string }}
 */
export function migrateCitation(text, diff, existsInOld) {
  const match = citationPattern.exec(text);
  if (!match) return { kind: 'not-a-citation', text };
  const [, cited, lines] = match;
  if (!isSourcePath(cited)) return { kind: 'not-a-citation', text };
  const resolved = resolveCitedPath(cited);
  if (resolved === null) return { kind: 'skipped', text, reason: 'outside the Base UI clone' };
  if (!existsInOld(resolved)) return { kind: 'unresolved', text, reason: `${resolved} is not in the old tree` };
  const fate = diff.get(resolved);
  if (fate === undefined) return { kind: 'unchanged', text };
  if (fate.newPath === null) return { kind: 'deleted', text, reason: `${resolved} was deleted` };
  const newCited = fate.newPath === resolved ? cited : citeLike(cited, fate.newPath);
  if (fate.binary) return { kind: 'flagged', text, reason: `${resolved} is binary and changed`, proposed: newCited };
  if (lines === undefined) {
    return newCited === cited ? { kind: 'unchanged', text } : { kind: 'moved', text: newCited };
  }
  const changedParts = [];
  const newLines = lines.replace(/(\d+)(?:-(\d+))?/g, (part, startText, endText) => {
    const start = Number(startText);
    const end = endText === undefined ? start : Number(endText);
    const mapped = mapRange({ start, end }, fate.hunks);
    if (mapped.changed) changedParts.push(part);
    return endText === undefined ? `${mapped.start}` : `${mapped.start}-${mapped.end}`;
  });
  const proposed = `${newCited}:${newLines}`;
  if (changedParts.length > 0) {
    return { kind: 'flagged', text, reason: `changed inside ${changedParts.join(', ')}`, proposed };
  }
  return proposed === text ? { kind: 'unchanged', text } : { kind: 'moved', text: proposed };
}

/**
 * Migrates the citations of one Markdown text. Only `Source:` lines are read, and on them only the part before `Test:`.
 * @param {string} markdown
 * @param {Map<string, object>} diff
 * @param {(resolved: string) => boolean} existsInOld
 * @returns {{ text: string, results: { line: number, from: string, kind: string, to: string, reason?: string, proposed?: string }[] }}
 */
export function migrateMarkdown(markdown, diff, existsInOld) {
  const results = [];
  const lines = markdown.split('\n');
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    const sourceAt = line.indexOf('Source:');
    if (sourceAt === -1) continue;
    const testAt = line.indexOf('Test:', sourceAt);
    const end = testAt === -1 ? line.length : testAt;
    const segment = line.slice(sourceAt, end).replace(/`([^`]+)`/g, (whole, span) => {
      const result = migrateCitation(span, diff, existsInOld);
      if (result.kind === 'not-a-citation') return whole;
      results.push({ line: index + 1, from: span, kind: result.kind, to: result.text, reason: result.reason, proposed: result.proposed });
      return `\`${result.text}\``;
    });
    lines[index] = line.slice(0, sourceAt) + segment + line.slice(end);
  }
  return { text: lines.join('\n'), results };
}

function git(repo, args) {
  return execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8', maxBuffer: 1 << 30 });
}

/**
 * Reads the rename-aware diff between two commits of the clone. Throws, naming the fetch to run, when one is missing.
 * @param {string} repo
 * @param {string} from
 * @param {string} to
 */
export function loadDiff(repo, from, to) {
  for (const ref of [from, to]) {
    try {
      git(repo, ['rev-parse', '--verify', '--quiet', `${ref}^{commit}`]);
    } catch {
      throw new Error(`${ref} is not in ${repo}; fetch it first: git -C ${repo} fetch --depth 1 origin tag ${ref}`);
    }
  }
  const diff = parseDiff(
    git(repo, ['-c', 'core.quotePath=false', 'diff', '--find-renames', '-l0', '--unified=0', '--no-color', '--no-ext-diff', from, to]),
  );
  const oldPaths = new Set(git(repo, ['-c', 'core.quotePath=false', 'ls-tree', '-r', '--name-only', from]).split('\n'));
  return { diff, existsInOld: (path) => oldPaths.has(path) };
}

function markdownFiles(root) {
  const found = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const path = join(root, entry.name);
    if (entry.isDirectory()) found.push(...markdownFiles(path));
    else if (entry.name.endsWith('.md')) found.push(path);
  }
  return found.sort();
}

/**
 * Runs the tool over files and returns the report (Markdown) and the counts per kind.
 * @param {{ repo: string, from: string, to: string, files: string[], write: boolean }} options
 */
export function run({ repo, from, to, files, write }) {
  const { diff, existsInOld } = loadDiff(repo, from, to);
  const counts = { unchanged: 0, moved: 0, flagged: 0, deleted: 0, unresolved: 0, skipped: 0 };
  const report = [`# Citation migration ${from} → ${to}`, ''];
  for (const file of files) {
    const before = readFileSync(file, 'utf8');
    const { text, results } = migrateMarkdown(before, diff, existsInOld);
    for (const result of results) counts[result.kind]++;
    const review = results.filter((result) => result.kind !== 'unchanged' && result.kind !== 'moved');
    const moved = results.filter((result) => result.kind === 'moved');
    if (review.length > 0 || moved.length > 0) {
      report.push(`## ${file}`, '');
      for (const result of review) {
        const proposed = result.proposed ? ` (proposed \`${result.proposed}\`)` : '';
        report.push(`- line ${result.line}: **${result.kind}** \`${result.from}\`: ${result.reason}${proposed}`);
      }
      for (const result of moved) report.push(`- line ${result.line}: moved \`${result.from}\` → \`${result.to}\``);
      report.push('');
    }
    if (write && text !== before) writeFileSync(file, text);
  }
  const total = Object.values(counts).reduce((sum, count) => sum + count, 0);
  report.splice(2, 0, `${total} citations: ${Object.entries(counts).map(([kind, count]) => `${count} ${kind}`).join(', ')}.`, '');
  return { report: report.join('\n'), counts };
}

function parseArguments(argv) {
  const options = { repo: 'reference/base-ui', from: null, to: null, write: false, files: [] };
  for (let index = 0; index < argv.length; index++) {
    const argument = argv[index];
    if (argument === '--repo') options.repo = argv[++index];
    else if (argument === '--from') options.from = argv[++index];
    else if (argument === '--to') options.to = argv[++index];
    else if (argument === '--write') options.write = true;
    else options.files.push(argument);
  }
  if (!options.from || !options.to) throw new Error('usage: migrate-citations.mjs --from <tag> --to <tag> [--repo <clone>] [--write] [files...]');
  if (options.files.length === 0) options.files = markdownFiles('docs');
  return options;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try {
    const { report, counts } = run(parseArguments(process.argv.slice(2)));
    process.stdout.write(`${report}\n`);
    process.exitCode = counts.flagged + counts.deleted + counts.unresolved > 0 ? 1 : 0;
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 2;
  }
}
