# Every game prefab with a ZNetView (a network object the save can hold), with what a new object of
# it needs in the save: persistent / distant flags and object type. Creatures and item drops are
# marked, so the editor can leave them out of what it offers to place. Saplings (Plant) also get
# their grow radius (free space they need to grow) and whether they need cultivated ground.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_prefabs.py <out.json>
import UnityPy, glob, json, sys
import os as _os
B=_os.path.join(_os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
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
        name=go['m_Name']
        if name in out: continue
        root=False; znv=None; creature=item=False; plant=None
        for c in go['m_Component']:
            t=byid.get(c['component']['m_PathID'])
            if not t: continue
            try: tt=t.read_typetree()
            except Exception: continue
            if t.type.name=='Transform': root=tt['m_Father']['m_PathID']==0
            elif t.type.name=='MonoBehaviour':
                if 'm_persistent' in tt and 'm_distant' in tt and 'm_type' in tt: znv=tt
                if 'm_runSpeed' in tt: creature=True
                if 'm_itemData' in tt: item=True
                if 'm_grownPrefabs' in tt and 'm_growRadius' in tt: plant=tt
        if root and znv:
            out[name]={'p':int(znv['m_persistent']),'d':int(znv['m_distant']),'t':int(znv['m_type']),**({'c':1} if creature else {}),**({'i':1} if item else {}),
                **({'gr':round(float(plant['m_growRadius']),3),'cult':int(plant.get('m_needCultivatedGround',0))} if plant else {})}
    if fi%100==0: print(fi,len(files),len(out),file=sys.stderr,flush=True)
json.dump(out,open(sys.argv[1],'w'),separators=(',',':'),sort_keys=True)
print('prefabs',len(out))
