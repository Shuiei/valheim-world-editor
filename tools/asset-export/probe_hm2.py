import UnityPy, glob, os, lz4.block
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
out='/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx/992e9bfc-61d8-4ba1-b402-d95d5d70ed95/scratchpad/terraintex/'
env=UnityPy.load(B+'10ba9da1')
hm=[o for o in env.objects if o.type.name=='MonoBehaviour']
ref=None
for o in hm:
    try: m=o.read_typetree()
    except Exception: continue
    if 'm_isDistantLod' in m and not m['m_isDistantLod']: ref=(o,m['m_material']); break
o,mref=ref
ext=o.assets_file.externals[mref['m_FileID']-1].path.split('/')[-1]
import sys; sys.path.insert(0,'/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx/992e9bfc-61d8-4ba1-b402-d95d5d70ed95/scratchpad')
import assetlib
assetlib._envs['10ba9da1']=(env,{x.path_id:x for x in env.objects})
o=assetlib._envs['10ba9da1'][1][o.path_id]
matobj=assetlib.resolve(o, mref)
by=assetlib.env_for_bundle_of(matobj)[1]
mat=by[mref['m_PathID']]; mt=mat.read_typetree()
print('MATERIAL', mt['m_Name'], 'keywords', mt.get('m_ValidKeywords') or mt.get('m_ShaderKeywords'))
sh=assetlib.resolve(mat, mt['m_Shader'])
props=mt['m_SavedProperties']
def tex_of(ref, envmap, ownerobj): return assetlib.resolve(ownerobj, ref)
for name,tex in props['m_TexEnvs']:
    if tex['m_Texture']['m_PathID']==0: print(' tex', name, None); continue
    t=tex_of(tex['m_Texture'], by, mat)
    if t:
        td=t.read(); print(' tex', name, td.m_Name, td.m_Width, 'x', td.m_Height, 'scale', tex['m_Scale'])
        try: td.image.save(out+f'{name}_{td.m_Name}.png')
        except Exception as ex: print('   export failed', ex)
for name,val in props['m_Floats']: print(' float', name, round(val,4))
for name,val in props['m_Colors']: print(' color', name, {k: round(v,4) for k,v in val.items()})
if sh:
    st=sh.read_typetree(); print('SHADER', st['m_ParsedForm']['m_Name'], 'platforms', st['platforms'])
    blob=bytes(st['compressedBlob'])
    def flat(x):
        while isinstance(x,list) and x and isinstance(x[0],list): x=[i for s in x for i in s]
        return x
    for pi,plat in enumerate(st['platforms']):
        if plat!=15: continue
        data=b''
        for o_,c,d in zip(flat(st['offsets'][pi]),flat(st['compressedLengths'][pi]),flat(st['decompressedLengths'][pi])):
            data+=lz4.block.decompress(blob[o_:o_+c], uncompressed_size=d)
        open(out+'terrain_shader_glcore.bin','wb').write(data); print('glcore program bytes', len(data))
else: print('shader external', mt['m_Shader'])
