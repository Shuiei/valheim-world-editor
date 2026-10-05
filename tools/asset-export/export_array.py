import sys; sys.path.insert(0,'/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx/992e9bfc-61d8-4ba1-b402-d95d5d70ed95/scratchpad')
import assetlib, UnityPy
from UnityPy.export import Texture2DConverter
from PIL import Image
out='/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx/992e9bfc-61d8-4ba1-b402-d95d5d70ed95/scratchpad/terraintex/'
# Texture2DArray stores a GraphicsFormat; map the compressed ones to TextureFormat.
GF={96:10,97:10,98:11,99:11,100:12,101:12,102:26,103:26,104:27,105:27,106:24,107:24,108:25,109:25,8:4,4:4}
e,objs=assetlib.env_for_bundle('c4210710')
for o in e.objects:
    if o.type.name!='Texture2DArray': continue
    t=o.read()
    if t.m_Name not in ('terrain_d_array','terrain_n_array'): continue
    tt=o.read_typetree()
    fmt=tt['m_Format']; w=tt['m_Width']; h=tt['m_Height']; depth=tt['m_Depth']; mips=tt.get('m_MipCount',1)
    data=bytes(tt['image data']) if tt.get('image data') else b''
    sd=tt.get('m_StreamData',{})
    if not data and sd.get('size'):
        res=sd['path'].split('/')[-1]
        for name,bf in e.files.items():
            for sub,sf in getattr(bf,'files',{}).items():
                if sub==res: sf.seek(sd['offset']) if hasattr(sf,'seek') else None; data=sf.bytes[sd['offset']:sd['offset']+sd['size']] if hasattr(sf,'bytes') else sf.read_bytes(sd['offset'],sd['size'])
    print(t.m_Name, 'format', fmt, w,'x',h,'slices',depth,'mips',mips,'bytes',len(data), 'datasize', tt.get('m_DataSize'))
    per=len(data)//depth
    for i in range(depth):
        chunk=data[i*per:(i+1)*per]
        img=Texture2DConverter.parse_image_data(chunk, w, h, UnityPy.enums.TextureFormat(GF.get(fmt, fmt)), (2022,3,0,0), 0, None, True) if hasattr(Texture2DConverter,'parse_image_data') else None
        if img is None: print('no decoder'); break
        img.save(out+f'{t.m_Name}_{i}.png')
    print('exported', depth)
