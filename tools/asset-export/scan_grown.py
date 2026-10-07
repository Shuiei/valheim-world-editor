# What each sapling grows into (Core/WorldGen/prefabs.json "grows"), from the game's Plant components
# (m_grownPrefabs): sapling_carrot -> Pickable_Carrot, Beech_Sapling -> Beech1... The editor gives the
# grown kinds their sapling's grow radius, for "Leave saplings and crops room to grow".
# Run after scan_prefabs.py, as it adds to its output.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_grown.py <prefabs.json>
import UnityPy, glob, json, os, sys, gc
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
files=sorted(glob.glob(B+'*'))
plants={}; want={}; cabof={}
# Pass 1: Plant components (on the sapling's root object) and their grown prefab references. Bundles
# are read one at a time: all of them at once do not fit in memory.
for f in files:
    try: env=UnityPy.load(f)
    except Exception: continue
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if not hasattr(sf,'objects'): continue
            cabof[name.lower()]=f
            for o in sf.objects.values():
                if o.type.name!='MonoBehaviour': continue
                try: d=o.read_typetree()
                except Exception: continue
                if 'm_grownPrefabs' not in d or 'm_growRadius' not in d: continue
                try: owner=sf.objects[d['m_GameObject']['m_PathID']].read_typetree()['m_Name']
                except Exception: continue
                refs=[]
                for p in d['m_grownPrefabs']:
                    fid,pid=p['m_FileID'],p['m_PathID']
                    if pid==0: continue
                    cab=name.lower() if fid==0 else os.path.basename(sf.externals[fid-1].path).lower()
                    refs.append((cab,pid)); want.setdefault(cab,set()).add(pid)
                plants[owner]=refs
    del env; gc.collect()
# Pass 2: the names of the grown prefabs.
names={}
for cab,pids in want.items():
    if cab not in cabof: continue
    env=UnityPy.load(cabof[cab])
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if name.lower()!=cab or not hasattr(sf,'objects'): continue
            for pid in pids:
                try: names[(cab,pid)]=sf.objects[pid].read_typetree()['m_Name']
                except Exception: pass
    del env; gc.collect()
prefabs=json.load(open(sys.argv[1]))
for v in prefabs.values(): v.pop('grows',None)
for owner,refs in plants.items():
    grown=sorted({names[r] for r in refs if r in names and names[r] in prefabs})
    if owner in prefabs and grown: prefabs[owner]['grows']=grown
json.dump(prefabs,open(sys.argv[1],'w'),separators=(',',':'),sort_keys=True)
for k,v in sorted(prefabs.items()):
    if 'grows' in v: print(k,v['gr'] if 'gr' in v else '-',v['grows'])
