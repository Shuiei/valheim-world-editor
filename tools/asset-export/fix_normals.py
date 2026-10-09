# Unity DXT5nm normal maps (x in A, y in G, R=1) -> plain RGB normal maps. Pillow only (no numpy),
# so the export runs with the small Python runtime shipped in the release.
import json, sys, os
from PIL import Image, ImageMath
OUT = sys.argv[1]
m = json.load(open(os.path.join(OUT, 'materials.json')))
# 1 - x^2 - y^2 (stored as 0..255) -> z mapped to a byte: sqrt, then 0..1 -> 128..255.
ZTABLE = [min(255, int((max(0.0, v / 255) ** 0.5 * 0.5 + 0.5) * 255 + 0.5)) for v in range(256)]
done = 0
for n in sorted(set(v['normal'] for v in m.values() if 'normal' in v)):
    p = os.path.join(OUT, 'tex', n); im = Image.open(p)
    if im.mode != 'RGBA': continue
    r, g, b, a = im.split()
    if r.getextrema()[0] < 0.97 * 255: continue
    x = ImageMath.lambda_eval(lambda e: e['float'](e['a']) / 127.5 - 1, a=a)
    y = ImageMath.lambda_eval(lambda e: e['float'](e['g']) / 127.5 - 1, g=g)
    q = ImageMath.lambda_eval(lambda e: e['max'](1 - e['x'] * e['x'] - e['y'] * e['y'], 0) * 255, x=x, y=y)
    z = q.convert('L').point(ZTABLE)
    # x, y back to bytes: (v * 0.5 + 0.5) * 255.
    xb = ImageMath.lambda_eval(lambda e: e['x'] * 127.5 + 128, x=x).convert('L')
    yb = ImageMath.lambda_eval(lambda e: e['y'] * 127.5 + 128, y=y).convert('L')
    # Written whole, then renamed: a run stopped here leaves the texture as it was.
    Image.merge('RGB', (xb, yb, z)).save(p + '.tmp', format='PNG' if p.endswith('.png') else 'JPEG', optimize=True); os.replace(p + '.tmp', p); done += 1
print('converted', done)
