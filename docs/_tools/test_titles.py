#!/usr/bin/env python3
"""Compare the test titles in a Base UI test directory with a requirements doc's §13 matrix.

Usage (from the repo root):
  python3 docs/_tools/test_titles.py docs/components/switch.md reference/base-ui/packages/react/src/switch
  python3 docs/_tools/test_titles.py docs/architecture/button-behavior.md path/to/file.test.tsx [more files or dirs]

Extracts every `it(` / `test(` / `it.each(…)(` / `it.skipIf(…)(` / `describe.for(…)(` title (and
`describe(` titles for context) from the given *.test.ts(x) files, then reports titles whose text does
not appear verbatim in the doc's "## 13." (or "## 4." for architecture docs) section. Parameterised
templates (`$key`, `%s`) are reported separately: the reviewer decides whether the matrix lists them
once with a variant count. This is a helper for the §13 completeness check, not a gate."""
import os, re, sys, glob

CALL = re.compile(r'\b(it|test|describe)((?:\.(?:each|for|skipIf|runIf|skip|only|todo|concurrent))*)\s*\(')
STR = re.compile(r'\s*([\'"`])')

def skip_balanced(s, i):
    """s[i] == '(' ; return index just after the matching ')'. Tolerates strings/templates/comments."""
    depth, n = 0, len(s)
    while i < n:
        c = s[i]
        if c in '\'"`':
            q = c; i += 1
            while i < n and s[i] != q:
                i += 2 if s[i] == '\\' else 1
            i += 1; continue
        if s.startswith('//', i):
            i = s.find('\n', i); i = n if i < 0 else i; continue
        if s.startswith('/*', i):
            i = s.find('*/', i); i = n if i < 0 else i + 2; continue
        if c == '(': depth += 1
        elif c == ')':
            depth -= 1
            if depth == 0: return i + 1
        i += 1
    return n

def read_string(s, i):
    m = STR.match(s, i)
    if not m: return None, i
    q = m.group(1); j = m.end(); out = []
    while j < len(s) and s[j] != q:
        if s[j] == '\\': out.append(s[j + 1]); j += 2
        else: out.append(s[j]); j += 1
    return ''.join(out), j + 1

def titles(path):
    s = open(path).read(); out = []
    for m in CALL.finditer(s):
        kind, mods = m.group(1), m.group(2)
        i = m.end()
        if mods:  # it.each([...])('title') / describe.for([...])('title') / it.skipIf(cond)('title')
            i = skip_balanced(s, i - 1)
            while i < len(s) and s[i] in ' \t\r\n': i += 1
            if i >= len(s) or s[i] != '(': continue
            i += 1
        t, _ = read_string(s, i)
        if t is None: continue
        out.append((kind + mods, t.strip()))
    return out

def section(doc):
    s = open(doc).read()
    m = re.search(r'^## 13\. .*?(?=^## 14\. )', s, re.M | re.S) or re.search(r'^## 4\. .*?(?=^## 5\. )', s, re.M | re.S)
    return (m.group(0) if m else s)

def main():
    if len(sys.argv) < 3: print(__doc__); sys.exit(2)
    doc, targets = sys.argv[1], sys.argv[2:]
    files = []
    for t in targets:
        files += sorted(glob.glob(os.path.join(t, '**', '*.test.ts*'), recursive=True)) if os.path.isdir(t) else [t]
    sec = section(doc); whole = open(doc).read()
    norm = lambda x: re.sub(r'\s+', ' ', x.replace('\\"', '"').replace("\\'", "'"))
    sec_n, whole_n = norm(sec), norm(whole)
    total = missing = 0
    for f in files:
        base = os.path.basename(f)
        ts = [(k, t) for k, t in titles(f) if not k.startswith('describe')]
        ds = [t for k, t in titles(f) if k.startswith('describe')]
        file_mentioned = base in whole
        print(f'\n### {f}  ({len(ts)} it/test titles; file name {"cited" if file_mentioned else "NOT cited"} in doc)')
        for k, t in ts:
            total += 1
            tn = norm(t)
            if tn in sec_n: continue
            where = 'elsewhere in doc' if tn in whole_n else 'NOT FOUND'
            flag = 'template' if ('$' in t or '%' in t) else 'missing'
            if flag == 'missing': missing += 1
            print(f'  [{flag:8}] [{where}] {k}: "{t}"')
        for d in ds:
            if norm(d) not in sec_n and d not in ('', ):
                print(f'  [describe ] not quoted in §13: "{d}"')
    print(f'\n{len(files)} files, {total} titles, {missing} plain titles not found in the §13 section')

if __name__ == '__main__': main()
