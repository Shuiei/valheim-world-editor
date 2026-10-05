# piece prefab name -> (bundle, path id of root GameObject)
import UnityPy, glob, json, sys, os
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
want=set(json.load(open('/home/thibs/Desktop/Valheim_BepInEx/_src/TerrainEditor/WorldGen/pieces.json')))
out={}
files=sorted(glob.glob(B+'*'))
for fi,f in enumerate(files):
    try: env=UnityPy.load(f)
    except Exception: continue
    byid={o.path_id:o for o in env.objects}
    for o in env.objects:
        if o.type.name!='GameObject': continue
        try: go=o.read_typetree()
        except Exception: continue
        n=go['m_Name']
        if n not in want or n in out: continue
        # root only
        for c in go['m_Component']:
            t=byid.get(c['component']['m_PathID'])
            if t and t.type.name=='Transform':
                if t.read_typetree()['m_Father']['m_PathID']==0: out[n]=[os.path.basename(f),o.path_id]
                break
    if fi%100==0: print(fi,len(out),file=sys.stderr,flush=True)
json.dump(out,open(sys.argv[1],'w'))
print('indexed',len(out),'of',len(want))
