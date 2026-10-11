# The game's location rules (Core/WorldGen/locations.json): where ZoneSystem may lay out each
# location (start temple, traders, bosses, dungeons, villages...) when a world first loads, and the alt
# biomes (Dark Meadows, Lox Plains...) that add or block some of them. SeedLocations replays the
# game's placement with these.
#
# The order is the game's: ZoneSystem's own m_locations, then the LocationLists in m_sortOrder (all
# share one: the game keeps the order they wake in, which its log shows as "Added N locations, M
# vegetations ... from main"), then the alt biomes' m_addLocations (ZoneSystem.SetupLocations).
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_locations.py <out.json>
import UnityPy, glob, json, os, sys, gc
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
# The lists' wake order, by (locations, vegetations), from the game's log (Valheim 0.221).
ORDER=[(3,0),(2,0),(27,25),(4,0),(25,33),(25,35)]
DROP={'m_prefab','m_foldout','m_prefabName'}
# The prefab's name, as the game's SoftReference gives it (m_prefab.Name): the file name the SoftRef
# manifest lists for its asset ID (m_prefabName is not always saved). The ID's hex is v3 v2 v1 v0.
NAMES={}
with open(os.path.join(os.path.dirname(os.path.dirname(B)), 'manifest'), encoding='utf-8-sig') as f:
    aid=None
    for line in f:
        line=line.strip()
        if line.startswith('- asset ID: '): aid=line[12:]
        elif line.startswith('path in bundle: ') and aid: NAMES[aid]=os.path.splitext(os.path.basename(line[16:]))[0]
def prefab_name(l):
    a=l['m_prefab']['m_assetID']
    return NAMES.get(''.join('%08x' % a[k] for k in ('v3','v2','v1','v0')), l['m_prefabName'])
def location(l, alt=None):
    out={'prefab': prefab_name(l)}
    out.update({k[2].lower()+k[3:]: v for k, v in l.items() if k not in DROP})
    if alt: out['altBiome']=alt
    return out
zone=None; lists={}; alts=None
for f in sorted(glob.glob(B+'*')):
    try: env=UnityPy.load(f)
    except Exception: continue
    for o in env.objects:
        if o.type.name!='MonoBehaviour': continue
        try: d=o.read_typetree()
        except Exception: continue
        if 'm_zoneSize' in d and 'm_locations' in d:
            # Some bundles hold a test scene's ZoneSystem too (fewer locations): the main one has most.
            if zone is None or len(d['m_locations'])>len(zone): zone=d['m_locations']
        elif 'm_sortOrder' in d and 'm_locations' in d:
            key=(len(d['m_locations']), len(d['m_vegetation']))
            if key in ORDER: lists.setdefault(key, d['m_locations'])
        elif 'm_alts' in d and alts is None:
            alts=d['m_alts']
    del env; gc.collect()
missing=[k for k in ORDER if k not in lists]
if zone is None or alts is None or missing:
    sys.exit(f'not found: zone={zone is not None} alts={alts is not None} lists={missing} (a game update? see the log for the new order)')
locs=[location(l) for l in zone]
for k in ORDER: locs+=[location(l) for l in lists[k]]
ALT_KEEP=['m_name','m_enabled','m_biome','m_minDistanceFromCenter','m_minAmountSpawned','m_maxAmountSpawned','m_chance',
    'm_requireNeighbor','m_notNeighbor','m_incompatibleAltBiomes','m_minEdgeSize','m_maxEdgeSize','m_minAvgHeight',
    'm_maxAvgHeight','m_belowWorldX','m_aboveWorldX','m_belowWorldY','m_aboveWorldY','m_blockLocationNames']
out_alts=[]
for a in alts:
    out_alts.append({k[2].lower()+k[3:]: a[k] for k in ALT_KEEP})
    locs+=[location(l, a['m_name']) for l in a['m_addLocations']]
with open(sys.argv[1],'w') as f:
    f.write('{"locations": [\n'+',\n'.join(json.dumps(l) for l in locs)+'\n],\n"altBiomes": [\n'+',\n'.join(json.dumps(a) for a in out_alts)+'\n]}\n')
print(len(locs), 'locations,', len(out_alts), 'alt biomes')
