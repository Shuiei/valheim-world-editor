# Union of several cobertura reports, per source file: lines run by any of them.
import sys, xml.etree.ElementTree as ET, collections
files = collections.defaultdict(dict)   # file -> {line: hit}
for path in sys.argv[1:]:
    for c in ET.parse(path).getroot().iter('class'):
        fn = c.get('filename').split('valheim-world-editor/')[-1]
        for l in c.iter('line'):
            n = int(l.get('number')); files[fn][n] = files[fn].get(n, False) or int(l.get('hits')) > 0
T = H = 0
rows = []
for fn, lines in files.items():
    if '/WorldGen/' in '/' + fn and not fn.endswith(('PieceCatalog.cs', 'PrefabCatalog.cs')): continue
    t = len(lines); h = sum(lines.values()); T += t; H += h
    rows.append((h / t if t else 1, t - h, t, fn))
for r, m, t, fn in sorted(rows):
    if t >= 15: print(f"{100*r:5.0f}% {m:5d} missed of {t:5d}  {fn}")
print(f"{100*H/T:.1f}% of {T} server lines (world generation excluded)")
