# Cut-out textures: give transparent pixels the colour of nearby opaque ones so mipmaps and
# filtering do not bleed the (often beige) hidden colour into leaf edges.
import json, sys, os
from PIL import Image
import numpy as np
OUT=sys.argv[1]
m=json.load(open(os.path.join(OUT,'materials.json')))
names=set(v['map'] for v in m.values() if 'map' in v and v['map'].endswith('.png') and ('cutoff' in v or v.get('transparent')))
done=0
for n in sorted(names):
    p=os.path.join(OUT,'tex',n); a=np.asarray(Image.open(p).convert('RGBA')).astype(np.float32)
    mask=a[...,3]>=128
    if mask.all() or not mask.any(): continue
    rgb=a[...,:3].copy(); filled=mask.copy()
    for it in range(16):
        acc=np.zeros_like(rgb); cnt=np.zeros(mask.shape,np.float32)
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
            f=np.roll(filled,(dy,dx),(0,1)); c=np.roll(rgb,(dy,dx),(0,1))
            acc+=c*f[...,None]; cnt+=f
        new=(cnt>0)&~filled
        if not new.any(): break
        rgb[new]=acc[new]/cnt[new][...,None]; filled|=new
    rgb[~filled]=a[...,:3][mask].mean(0)
    out=np.concatenate([rgb,a[...,3:4]],-1)
    Image.fromarray(np.clip(out+0.5,0,255).astype(np.uint8),'RGBA').save(p,optimize=True); done+=1
print('bled',done)
