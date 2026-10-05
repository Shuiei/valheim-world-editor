# Cut-out textures: give transparent pixels the colour of nearby opaque ones so mipmaps and
# filtering do not bleed the (often beige) hidden colour into leaf edges. Pillow only (no numpy),
# so the export runs with the small Python runtime shipped in the release.
import json, sys, os
from PIL import Image, ImageFilter, ImageMath, ImageStat
OUT = sys.argv[1]
m = json.load(open(os.path.join(OUT, 'materials.json')))
names = set(v['map'] for v in m.values() if 'map' in v and v['map'].endswith('.png') and ('cutoff' in v or v.get('transparent')))
# Sum of the 3 x 3 neighbourhood, divided by 9 (Pillow filters work on 8-bit images).
AVG = ImageFilter.Kernel((3, 3), [1] * 9, scale=9)
PAD = 16   # colour spreads at most 16 pixels; textures tile, so the edges wrap around

def wrap_pad(img, k):
    w, h = img.size
    out = Image.new(img.mode, (w + 2 * k, h + 2 * k))
    for dx in (-w, 0, w):
        for dy in (-h, 0, h): out.paste(img, (k + dx, k + dy))
    return out

done = 0
for n in sorted(names):
    p = os.path.join(OUT, 'tex', n)
    orig = Image.open(p).convert('RGBA')
    k = min(PAD, orig.size[0], orig.size[1])
    im = wrap_pad(orig, k)
    alpha = im.getchannel('A')
    mask = alpha.point(lambda v: 255 if v >= 128 else 0)
    lo, hi = mask.getextrema()
    if lo == 255 or hi == 0: continue
    bands = list(im.split()[:3])
    filled = mask
    for it in range(16):
        # Average colour of the filled neighbours: blur (colour x filled) and blur (filled), divide.
        den = filled.filter(AVG)
        new = ImageMath.lambda_eval(lambda e: (e['d'] > 0) & (e['f'] == 0), d=den, f=filled).convert('L')
        if new.getextrema()[1] == 0: break
        for i, c in enumerate(bands):
            num = ImageMath.lambda_eval(lambda e: e['c'] * e['f'] / 255, c=c, f=filled).convert('L').filter(AVG)
            val = ImageMath.lambda_eval(lambda e: e['float'](e['nu']) * 255 / e['max'](e['de'], 1), nu=num, de=den).convert('L')
            bands[i] = Image.composite(val, c, new.point(lambda v: 255 if v else 0))
        filled = ImageMath.lambda_eval(lambda e: e['max'](e['f'], e['nw'] * 255), f=filled, nw=new).convert('L')
    # Pixels too far from any opaque one: the average opaque colour.
    if filled.getextrema()[0] == 0:
        mean = ImageStat.Stat(im.convert('RGB'), mask).mean
        far = filled.point(lambda v: 255 if v == 0 else 0)
        bands = [Image.composite(Image.new('L', im.size, int(mean[i] + 0.5)), c, far) for i, c in enumerate(bands)]
    w, h = orig.size
    Image.merge('RGBA', bands + [alpha]).crop((k, k, k + w, k + h)).save(p, optimize=True); done += 1
print('bled', done)
