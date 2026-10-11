# What item stands and armour stands can hold (Core/WorldGen/prefabs.json), from the game's ItemDrop,
# ItemStand and ArmorStand components. Items get their type ("it", ItemDrop.ItemData.ItemType), their
# attach override ("ao", when set), their number of variants ("var", when they have some) and whether
# they have an "attach" child ("at": ItemStand.GetAttachPrefab, without one no item stand takes them)
# or an "attach_skin" one ("ats": armour stands take either, or a chest or legs piece without), and
# for items that wear, their durability when new ("dur") and what each quality level adds ("durl").
# Item stands get the types they take ("is") and the items they take or refuse whatever their type
# ("isi", "isu"); armour stands the types each slot takes ("as", in slot order: the save keeps slot i's
# item under "<i>_item"). Run after scan_prefabs.py, as it adds to its output.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_stands.py <prefabs.json>
import UnityPy, glob, json, os, sys, gc
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
files=sorted(glob.glob(B+'*'))
items={}; owners={}; stands={}; armour={}

def ref(sf, name, p):
    fid,pid=p['m_FileID'],p['m_PathID']
    if pid==0: return None
    return (name.lower() if fid==0 else os.path.basename(sf.externals[fid-1].path).lower(), pid)

def attach_children(sf, go):
    found=set()
    for c in go['m_Component']:
        t=sf.objects.get(c['component']['m_PathID'])
        if t is None or t.type.name!='Transform': continue
        for ch in t.read_typetree()['m_Children']:
            ct=sf.objects.get(ch['m_PathID'])
            if ct is None: continue
            cg=sf.objects.get(ct.read_typetree()['m_GameObject']['m_PathID'])
            if cg is not None and cg.read_typetree()['m_Name'] in ('attach','attach_skin'): found.add(cg.read_typetree()['m_Name'])
    return found

# Bundles are read one at a time: all of them at once do not fit in memory.
for fi,f in enumerate(files):
    try: env=UnityPy.load(f)
    except Exception: continue
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if not hasattr(sf,'objects'): continue
            for pid,o in sf.objects.items():
                if o.type.name!='MonoBehaviour': continue
                try: d=o.read_typetree()
                except Exception: continue
                is_item='m_itemData' in d and 'm_autoPickup' in d
                is_stand='m_supportedTypes' in d and 'm_attachOther' in d
                is_armour='m_slots' in d and 'm_poseCount' in d
                if not (is_item or is_stand or is_armour): continue
                try:
                    go=sf.objects[d['m_GameObject']['m_PathID']].read_typetree()
                    owner=go['m_Name']
                except Exception: continue
                if is_item:
                    owners[(name.lower(),pid)]=owner
                    if owner in items: continue
                    sh=d['m_itemData']['m_shared']
                    e={'it':int(sh['m_itemType'])}
                    if int(sh.get('m_attachOverride',0)): e['ao']=int(sh['m_attachOverride'])
                    if int(sh.get('m_variants',0))>0: e['var']=int(sh['m_variants'])
                    if int(sh.get('m_useDurability',0)):
                        e['dur']=round(float(sh['m_maxDurability']),2)
                        if float(sh.get('m_durabilityPerLevel',0)): e['durl']=round(float(sh['m_durabilityPerLevel']),2)
                    try:
                        kids=attach_children(sf, go)
                        if 'attach' in kids: e['at']=1
                        if 'attach_skin' in kids: e['ats']=1
                    except Exception: pass
                    items[owner]=e
                elif is_stand and owner not in stands:
                    stands[owner]=(sorted({int(t) for t in d['m_supportedTypes']}),
                        [r for r in (ref(sf,name,p) for p in d.get('m_supportedItems',[])) if r],
                        [r for r in (ref(sf,name,p) for p in d.get('m_unsupportedItems',[])) if r])
                elif is_armour and owner not in armour:
                    armour[owner]=[sorted({int(t) for t in s['m_supportedTypes']}) for s in d['m_slots']]
    del env; gc.collect()
    if fi%100==0: print(fi,len(files),len(items),len(stands),len(armour),file=sys.stderr,flush=True)

prefabs=json.load(open(sys.argv[1]))
for v in prefabs.values():
    for k in ('it','ao','var','at','ats','dur','durl','is','isi','isu','as'): v.pop(k,None)
for n,e in items.items():
    if n in prefabs: prefabs[n].update(e)
for n,(types,sup,uns) in stands.items():
    if n not in prefabs: continue
    prefabs[n]['is']=types
    names=lambda refs: sorted({owners[r] for r in refs if r in owners})
    if sup: prefabs[n]['isi']=names(sup)
    if uns: prefabs[n]['isu']=names(uns)
for n,slots in armour.items():
    if n in prefabs: prefabs[n]['as']=slots
json.dump(prefabs,open(sys.argv[1],'w'),separators=(',',':'),sort_keys=True)
print('items',sum(1 for v in prefabs.values() if 'it' in v),'item stands',sorted(n for n,v in prefabs.items() if 'is' in v),'armour stands',sorted(n for n,v in prefabs.items() if 'as' in v))
