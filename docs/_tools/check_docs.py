#!/usr/bin/env python3
"""Lint the requirements docs: ID uniqueness, gap-free numbering, Source/Test presence,
dangling cross-references, React vocabulary in requirement statements, index/body agreement,
and README status vs files on disk. Run from the repo root: python3 docs/_tools/check_docs.py"""
import re, glob, collections, os, sys
ROOT = os.path.join(os.path.dirname(__file__), '..')
files = sorted(glob.glob(f'{ROOT}/architecture/*.md') + glob.glob(f'{ROOT}/components/*.md'))
ID = re.compile(r'\*\*([A-Z]{3})-([A-Z0-9]{2,4})-(\d{2,3})\*\* — (MUST(?: NOT)?|SHOULD(?: NOT)?|MAY)')
REF = re.compile(r'\b([A-Z]{3}-[A-Z0-9]{2,4}-\d{2,3})\b')
REACT = re.compile(r'\b(useEffect|useLayoutEffect|useRef|useState|useCallback|useMemo|React\.|StrictMode)\b')
defined, prefix_of, problems = {}, {}, collections.defaultdict(list)
docs = {}
for f in files:
    rel = os.path.relpath(f, ROOT); s = open(f).read(); docs[rel] = s
    ids = [(f'{p}-{a}-{n}', p, a, int(n)) for p, a, n, _ in ID.findall(s)]
    pfx = collections.Counter(p for _, p, _, _ in ids)
    if len(pfx) > 1: problems[rel].append(f'multiple prefixes defined: {dict(pfx)}')
    for i, p, a, n in ids:
        if i in defined: problems[rel].append(f'duplicate ID {i} (also in {defined[i]})')
        defined[i] = rel; prefix_of.setdefault(p, rel)
    by = collections.defaultdict(list)
    for i, p, a, n in ids: by[(p, a)].append(n)
    for (p, a), ns in by.items():
        gaps = sorted(set(range(1, max(ns) + 1)) - set(ns))
        if gaps:
            # allow withdrawn IDs listed in the index
            gaps = [g for g in gaps if not re.search(rf'{p}-{a}-{g:02d}\*\* — Withdrawn|{p}-{a}-{g:02d}\s*\|[^|]*\|[^|]*Withdrawn', s)]
            if gaps: problems[rel].append(f'gaps in {p}-{a}: {gaps}')
    # Source/Test and React terms per requirement block
    for m in re.finditer(r'- \*\*([A-Z]{3}-[A-Z0-9]{2,4}-\d{2,3})\*\* — (?:MUST|SHOULD|MAY)[^\n]*(?:\n(?!- \*\*)[^\n]*)*', s):
        block = m.group(0); i = m.group(1)
        stmt = block.split('Source:')[0]
        if 'Source:' not in block or 'Test:' not in block: problems[rel].append(f'{i}: missing Source/Test')
        if REACT.search(stmt): problems[rel].append(f'{i}: React API name in statement')
    # index agreement
    idx = re.search(r'^## (?:15|9)\. Requirement index(.*?)(?=^## |\Z)', s, re.M | re.S)
    if not idx: problems[rel].append('no requirement index section')
    else:
        in_idx = set(re.findall(r'\|\s*([A-Z]{3}-[A-Z0-9]{2,4}-\d{2,3})\s*\|', idx.group(1)))
        body = {i for i, *_ in ids}
        miss = sorted(body - in_idx); extra = sorted(in_idx - body)
        if miss: problems[rel].append(f'{len(miss)} IDs missing from index e.g. {miss[:5]}')
        if extra:
            extra = [e for e in extra if not re.search(rf'{e}\*\* — Withdrawn|{e}\s*\|[^|]*\|[^|]*Withdrawn', s)]
            if extra: problems[rel].append(f'{len(extra)} index IDs not defined in body e.g. {extra[:5]}')
    if not re.search(r'^## (?:16|10)\. Open questions', s, re.M): problems[rel].append('no open questions section')
# dangling cross-references (only for prefixes that have a doc)
for rel, s in docs.items():
    refs = set(REF.findall(s))
    withdrawn = set(re.findall(r'\*\*([A-Z]{3}-[A-Z0-9]{2,4}-\d{2,3})\*\* — Withdrawn', s))
    dang = sorted(r for r in refs if r not in defined and r not in withdrawn and r.split('-')[0] in prefix_of)
    if dang: problems[rel].append(f'{len(dang)} dangling refs e.g. {dang[:8]}')
# README vs disk
readme = open(f'{ROOT}/README.md').read()
for rel in docs:
    row = re.search(rf'\| {re.escape(rel)} \| \w+ \| ([^|]+) \|', readme)
    if not row: problems[rel].append('not in README status table')
    elif not any(v in row.group(1) for v in ('draft', 'reviewed', 'approved', 'implemented')):
        problems[rel].append(f'README status is "{row.group(1).strip()}" but file exists')
for m in re.finditer(r'\| ((?:architecture|components)/[\w-]+\.md) \| \w+ \| ([^|]+) \|', readme):
    if m.group(1) not in docs and 'draft' in m.group(2): problems[m.group(1)].append('README says draft but file missing')
total = sum(1 for _ in defined)
untagged = sum(len(re.findall(r'Test: (?!\[L[1-4]\]|none|see )', s)) for s in docs.values())
print(f'{len(docs)} docs, {total} requirements, {len(prefix_of)} prefixes; untagged Test lines: {untagged}')
for rel in sorted(problems):
    print(f'\n{rel}'); [print('  -', p) for p in problems[rel]]
if not problems: print('no problems found')
sys.exit(1 if problems else 0)
