# The game's vegetation rules (WorldGen/vegetation.json): ZoneSystem's m_vegetation, then the
# LocationLists' (in m_sortOrder), as ZoneSystem.SetupLocations merges them. "Regrow nature" places
# by these rules like ZoneSystem.PlaceVegetation. Alt biomes' extra vegetation (Deep North parts the
# editor's generator does not model) is left out.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_vegetation.py <out.json>
import UnityPy, glob, json, os, sys, gc
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
files=sorted(glob.glob(B+'*'))
zone=None; lists={}; want={}; cabof={}
def refs_of(sf, name, vegs):
    out=[]
    for v in vegs:
        p=v['m_prefab']; fid,pid=p['m_FileID'],p['m_PathID']
        cab=name.lower() if fid==0 else os.path.basename(sf.externals[fid-1].path).lower()
        want.setdefault(cab,set()).add(pid); out.append((v,(cab,pid)))
    return out
# Pass 1: the ZoneSystem and the LocationLists (each once: some bundles repeat them).
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
                if 'm_vegetation' not in d: continue
                if 'm_zoneSize' in d:
                    if zone is None: zone=refs_of(sf,name,d['m_vegetation'])
                elif 'm_sortOrder' in d and d['m_vegetation']:
                    key=(d['m_sortOrder'], d['m_vegetation'][0]['m_name'], len(d['m_vegetation']))
                    if key not in lists: lists[key]=refs_of(sf,name,d['m_vegetation'])
    del env; gc.collect()
# Random draws a prefab's components make in Awake when the game creates it (they shift the
# generator's sequence for the rest of the rule): LodFadeInOut (with a LODGroup, the camera more than
# 20 m away: the usual case while zones generate around a player), MineRock5, Floating, LightFlicker,
# Fish one each, RandomFlyingBird two, Pickable one when it respawns (m_respawnTimeMinutes > 0). Checked
# against a played world: with these, the generated spots match the saved ones. Components of
# inactive children do not wake.
DRAWS={'LodFadeInOut':1,'MineRock5':1,'Floating':1,'LightFlicker':1,'Fish':1,'RandomFlyingBird':2}
def script_ref(sf, cab, mb):
    p=mb.get('m_Script') or {}
    fid,pid=p.get('m_FileID',0),p.get('m_PathID',0)
    if not pid: return None
    return (cab if fid==0 else os.path.basename(sf.externals[fid-1].path).lower(), pid)
# The scripts on a prefab's active objects: [(script ref, LODGroup on the same object, respawns)].
def scripts_of(sf, cab, go_pid):
    out=[]; stack=[go_pid]
    while stack:
        g=sf.objects.get(stack.pop())
        if g is None: continue
        try: go=g.read_typetree()
        except Exception: continue
        if go.get('m_IsActive',1)!=1 and g.path_id!=go_pid: continue
        comps=[sf.objects.get(c['component']['m_PathID']) for c in go['m_Component']]
        comps=[c for c in comps if c is not None]
        has_lod=any(co.type.name=='LODGroup' for co in comps)
        for co in comps:
            if co.type.name=='Transform':
                try:
                    for ch in co.read_typetree()['m_Children']:
                        t=sf.objects.get(ch['m_PathID'])
                        if t: stack.append(t.read_typetree()['m_GameObject']['m_PathID'])
                except Exception: pass
            elif co.type.name=='MonoBehaviour':
                try: mb=co.read_typetree(); r=script_ref(sf, cab, mb)
                except Exception: r=None; mb={}
                if r: out.append((r,has_lod,mb.get('m_respawnTimeMinutes',0)>0))
    return out
# Pass 2: prefab names and their scripts.
names={}; scripts={}
for cab,pids in want.items():
    if cab not in cabof: continue
    env=UnityPy.load(cabof[cab])
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if name.lower()!=cab or not hasattr(sf,'objects'): continue
            for pid in pids:
                try:
                    names[(cab,pid)]=sf.objects[pid].read_typetree()['m_Name']
                    scripts[(cab,pid)]=scripts_of(sf,cab,pid)
                except Exception: pass
    del env; gc.collect()
# Pass 3: script class names (MonoScripts often live in other bundles).
script_names={}; swant={}
for lst in scripts.values():
    for (c,p),_,_ in lst: swant.setdefault(c,set()).add(p)
for cab,pids in swant.items():
    if cab not in cabof: continue
    env=UnityPy.load(cabof[cab])
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if name.lower()!=cab or not hasattr(sf,'objects'): continue
            for pid in pids:
                try: script_names[(cab,pid)]=sf.objects[pid].read_typetree()['m_ClassName']
                except Exception: pass
    del env; gc.collect()
KEEP=['m_name','m_enable','m_min','m_max','m_forcePlacement','m_scaleMin','m_scaleMax','m_randTilt','m_chanceToUseGroundTilt','m_biome','m_biomeArea','m_blockCheck','m_snapToStaticSolid','m_minAltitude','m_maxAltitude','m_minVegetation','m_maxVegetation','m_surroundCheckVegetation','m_surroundCheckDistance','m_surroundCheckLayers','m_surroundBetterThanAverage','m_minOceanDepth','m_maxOceanDepth','m_minTilt','m_maxTilt','m_terrainDeltaRadius','m_maxTerrainDelta','m_minTerrainDelta','m_snapToWater','m_groundOffset','m_groupSizeMin','m_groupSizeMax','m_groupRadius','m_minDistanceFromCenter','m_maxDistanceFromCenter','m_inForest','m_forestTresholdMin','m_forestTresholdMax']
out=[]
for entries in [zone or []]+[lists[k] for k in sorted(lists)]:
    for v,r in entries:
        e={k[2:]:v[k] for k in KEEP if k in v}
        e['prefab']=names.get(r)
        def one(sr,lod,respawns):
            n=script_names.get(sr)
            if n=='LodFadeInOut': return 1 if lod else 0
            if n=='Pickable': return 1 if respawns else 0
            return DRAWS.get(n,0)
        e['draws']=sum(one(*x) for x in scripts.get(r,[]))
        if e['prefab']: out.append(e)
json.dump(out,open(sys.argv[1],'w'),indent=0)
print({e['prefab']:e['draws'] for e in out})
print(len(out),'rules;',sum(1 for e in out if e['enable']),'enabled; lists',sorted(lists))
