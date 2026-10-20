#!/usr/bin/env python3
"""Apply test-layer tags to the Test: lines of one requirements doc (implementation revision, ADR 06).

Usage (from the repo root):
  python3 docs/_tools/apply_layers.py <doc.md> <mapping.txt> [--dry]

mapping.txt lines (comments with #):
  DEFAULT <AREA> <L1|L2|L3|L4>     default layer for every ID of that AREA in this doc
  <PFX-AREA-NN> <L1|L2|L3|L4>      override for one ID
A tagged line reads `Test: [L2] ...`. Lines reading `Test: none` (or `Test: see <ID>`) are left untagged.
Already-tagged lines are left alone. Prints the IDs that received no layer so the reviewer can add them."""
import re, sys
LAYERS={'L1','L2','L3','L4'}
def main():
    if len(sys.argv)<3: print(__doc__); sys.exit(2)
    doc, mapping = sys.argv[1], sys.argv[2]; dry='--dry' in sys.argv
    defaults, overrides = {}, {}
    for raw in open(mapping):
        line=raw.split('#')[0].strip()
        if not line: continue
        parts=line.split()
        if parts[0]=='DEFAULT' and len(parts)==3 and parts[2] in LAYERS: defaults[parts[1]]=parts[2]
        elif len(parts)==2 and parts[1] in LAYERS: overrides[parts[0]]=parts[1]
        else: print('bad mapping line:', raw.rstrip()); sys.exit(2)
    s=open(doc).read()
    block=re.compile(r'(- \*\*([A-Z]{3})-([A-Z0-9]{2,4})-(\d{2,3})\*\* — (?:MUST|SHOULD|MAY)[^\n]*(?:\n(?!- \*\*)[^\n]*)*)')
    tagged=skipped=0; missing=[]; already=0
    def fix(m):
        nonlocal tagged, skipped, already
        b=m.group(1); pid=f'{m.group(2)}-{m.group(3)}-{m.group(4)}'; area=m.group(3)
        tm=re.search(r'Test: ', b)
        if not tm: return b
        after=b[tm.end():tm.end()+12]
        if after.startswith('['): already+=1; return b
        if after.startswith('none') or after.startswith('see '): skipped+=1; return b
        layer=overrides.get(pid) or defaults.get(area)
        if not layer: missing.append(pid); return b
        tagged+=1
        return b[:tm.end()]+f'[{layer}] '+b[tm.end():]
    out=block.sub(fix, s)
    if not dry: open(doc,'w').write(out)
    print(f'{doc}: tagged {tagged}, already tagged {already}, untagged none/see {skipped}, no layer for {len(missing)}' + (f': {missing[:40]}' if missing else ''))
if __name__=='__main__': main()
