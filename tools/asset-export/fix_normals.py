# Unity DXT5nm normal maps (x in A, y in G, R=1) -> plain RGB normal maps.
import json, sys, os, math
from PIL import Image
import numpy as np
OUT=sys.argv[1]
m=json.load(open(os.path.join(OUT,'materials.json')))
done=0
for n in sorted(set(v['normal'] for v in m.values() if 'normal' in v)):
    p=os.path.join(OUT,'tex',n); im=Image.open(p)
    if im.mode!='RGBA': continue
    a=np.asarray(im).astype(np.float32)/255
    if a[...,0].min()<0.97: continue
    x=a[...,3]*2-1; y=a[...,1]*2-1; z=np.sqrt(np.clip(1-x*x-y*y,0,1))
    rgb=np.stack([x*0.5+0.5,y*0.5+0.5,z*0.5+0.5],-1)
    Image.fromarray((rgb*255+0.5).astype(np.uint8),'RGB').save(p, optimize=True); done+=1
print('converted',done)
