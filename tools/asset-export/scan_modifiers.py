# Terrain modifiers of locations and pieces (Core/WorldGen/terrain-modifiers.json, in git).
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_modifiers.py <out.json>
import UnityPy, glob, json, math, sys
import os as _os
B=_os.path.join(_os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
def qmul(a,b):
    ax,ay,az,aw=a; bx,by,bz,bw=b
    return (aw*bx+ax*bw+ay*bz-az*by, aw*by-ax*bz+ay*bw+az*bx, aw*bz+ax*by-ay*bx+az*bw, aw*bw-ax*bx-ay*by-az*bz)
def qrot(q,v):
    x,y,z,w=q; vx,vy,vz=v
    # v' = q * v * q^-1
    ix= w*vx + y*vz - z*vy; iy= w*vy + z*vx - x*vz; iz= w*vz + x*vy - y*vx; iw=-x*vx - y*vy - z*vz
    return (ix*w + iw*-x + iy*-z - iz*-y, iy*w + iw*-y + iz*-x - ix*-z, iz*w + iw*-z + ix*-y - iy*-x)
result={}
files=sorted(glob.glob(B+'*'))
for fi,f in enumerate(files):
    try: env=UnityPy.load(f)
    except Exception: continue
    byid={o.path_id:o for o in env.objects}
    cache={}
    def tt(pid):
        if pid not in cache:
            o=byid.get(pid); cache[pid]=(o.type.name, o.read_typetree()) if o else (None,None)
        return cache[pid]
    def kind(m):
        if 'm_levelOffset' in m and 'm_smoothPower' in m: return 'mod'
        if 'm_exteriorRadius' in m and 'm_noBuild' in m: return 'loc'
        if 'm_persistent' in m and 'm_distant' in m and 'm_type' in m: return 'znv'
        return None
    for o in env.objects:
        if o.type.name!='GameObject': continue
        try: go=o.read_typetree()
        except Exception: continue
        comps=[tt(c['component']['m_PathID']) for c in go['m_Component']]
        tr=[c for c in comps if c[0]=='Transform']
        if not tr or tr[0][1]['m_Father']['m_PathID']!=0: continue   # roots only
        kinds={kind(c[1]) for c in comps if c[0]=='MonoBehaviour'}
        if not ({'loc','znv'} & kinds): continue
        mods=[]
        def walk(gopid, pos, rot, scale, active):
            t,g=tt(gopid)
            if g is None: return
            act = active and g.get('m_IsActive',1)==1
            trc=None; ms=[]
            for c in g['m_Component']:
                ct,cm=tt(c['component']['m_PathID'])
                if ct=='Transform': trc=cm
                elif ct=='MonoBehaviour' and kind(cm)=='mod': ms.append(cm)
            if trc is None: return
            lp=trc['m_LocalPosition']; lr=trc['m_LocalRotation']; ls=trc['m_LocalScale']
            p=(lp['x']*scale[0],lp['y']*scale[1],lp['z']*scale[2]); p=qrot(rot,p); p=(p[0]+pos[0],p[1]+pos[1],p[2]+pos[2])
            r=qmul(rot,(lr['x'],lr['y'],lr['z'],lr['w'])); s=(scale[0]*ls['x'],scale[1]*ls['y'],scale[2]*ls['z'])
            if gopid==o.path_id: p,r,s=(0,0,0),(0,0,0,1),(1,1,1)   # root frame
            for m in ms:
                if act and m.get('m_Enabled',1)==1:
                    mods.append({'pos':[round(v,4) for v in p],'level':m['m_level'],'levelOffset':m['m_levelOffset'],'levelRadius':m['m_levelRadius'],'square':m['m_square'],
                        'smooth':m['m_smooth'],'smoothRadius':m['m_smoothRadius'],'smoothPower':m['m_smoothPower'],'paintCleared':m['m_paintCleared'],'paintType':m['m_paintType'],
                        'paintRadius':m['m_paintRadius'],'paintStrength':m.get('m_paintStrength',1.0),'paintHeightCheck':m['m_paintHeightCheck'],'sortOrder':m['m_sortOrder'],
                        'player':m['m_playerModifiction'],'useTerrainCompiler':m.get('m_useTerrainCompiler',0)})
            for ch in trc['m_Children']:
                ct,cm=tt(ch['m_PathID'])
                if cm: walk(cm['m_GameObject']['m_PathID'], p, r, s, act)
        walk(o.path_id,(0,0,0),(0,0,0,1),(1,1,1),True)
        if mods:
            result[go['m_Name']]={'location':'loc' in kinds,'modifiers':mods}
    if fi%50==0: print('scanned',fi,'of',len(files),'found',len(result),file=sys.stderr)
json.dump(result,open(sys.argv[1],'w'),indent=0)
print('prefabs with terrain modifiers:',len(result),'locations:',sum(1 for v in result.values() if v['location']))
for n,v in sorted(result.items())[:400]:
    if v['location']: print(' L', n, len(v['modifiers']), [ (m['levelRadius'] if m['level'] else 0, m['smoothRadius'] if m['smooth'] else 0) for m in v['modifiers']][:3])
for n,v in sorted(result.items()):
    if not v['location']: print(' P', n, len(v['modifiers']), 'player' if v['modifiers'][0]['player'] else '')
