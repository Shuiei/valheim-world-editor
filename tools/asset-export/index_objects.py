import UnityPy, glob, json, sys, os
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
def sh(s):
    a=b=5381; i=0; n=len(s)
    while i<n:
        a=((a<<5)+a ^ ord(s[i]))&0xffffffff
        if i==n-1: break
        b=((b<<5)+b ^ ord(s[i+1]))&0xffffffff
        i+=2
    v=(a+b*1566083941)&0xffffffff
    return v-(1<<32) if v>=1<<31 else v
want={x['prefab']:x['count'] for x in json.load(open(sys.argv[1]))}
out={}
for fi,f in enumerate(sorted(glob.glob(B+'*'))):
    try: env=UnityPy.load(f)
    except Exception: continue
    byid={o.path_id:o for o in env.objects}
    for o in env.objects:
        if o.type.name!='GameObject': continue
        try: go=o.read_typetree()
        except Exception: continue
        h=sh(go['m_Name'])
        # Several root objects can share a name (a model asset and the game prefab built from it):
        # keep scanning until the real prefab, the one with a ZNetView, is found.
        if h not in want or out.get(str(h), {}).get('znv'): continue
        kinds=[]; root=False; znv=False
        for c in go['m_Component']:
            t=byid.get(c['component']['m_PathID'])
            if not t: continue
            if t.type.name=='Transform':
                root=t.read_typetree()['m_Father']['m_PathID']==0
            elif t.type.name=='MonoBehaviour':
                try:
                    tt=t.read_typetree()
                    if 'm_persistent' in tt and 'm_type' in tt: znv=True
                    for k in ('m_health','m_minToolTier','m_itemData','m_runSpeed','m_respawnTimeMinutes','m_logPrefab','m_lodLevel','m_spawnOnHit'):
                        if k in tt: kinds.append(k)
                except Exception: pass
            else: kinds.append(t.type.name)
        if root and (str(h) not in out or znv): out[str(h)]={'name':go['m_Name'],'bundle':os.path.basename(f),'pid':o.path_id,'count':want[h],'kinds':kinds,'znv':znv}
    if fi%100==0: print(fi,len(out),file=sys.stderr,flush=True)
json.dump(out,open(sys.argv[2],'w'),indent=0)
print('named',len(out),'of',len(want))
