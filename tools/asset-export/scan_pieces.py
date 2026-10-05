# Build-piece catalogue (WorldGen/pieces.json, in git): category and footprint of every piece.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_pieces.py <out.json>
import UnityPy, glob, json, sys
import os as _os
B=_os.path.join(_os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
def qmul(a,b):
    ax,ay,az,aw=a; bx,by,bz,bw=b
    return (aw*bx+ax*bw+ay*bz-az*by, aw*by-ax*bz+ay*bw+az*bx, aw*bz+ax*by-ay*bx+az*bw, aw*bw-ax*bx-ay*by-az*bz)
def qrot(q,v):
    x,y,z,w=q; vx,vy,vz=v
    ix= w*vx + y*vz - z*vy; iy= w*vy + z*vx - x*vz; iz= w*vz + x*vy - y*vx; iw=-x*vx - y*vy - z*vz
    return (ix*w + iw*-x + iy*-z - iz*-y, iy*w + iw*-y + iz*-x - ix*-z, iz*w + iw*-z + ix*-y - iy*-x)
out={}
files=sorted(glob.glob(B+'*'))
for fi,f in enumerate(files):
    try: env=UnityPy.load(f)
    except Exception: continue
    byid={o.path_id:o for o in env.objects}
    cache={}
    def tt(pid):
        if pid not in cache:
            o=byid.get(pid)
            try: cache[pid]=(o.type.name, o.read_typetree()) if o else (None,None)
            except Exception: cache[pid]=(None,None)
        return cache[pid]
    for o in env.objects:
        if o.type.name!='GameObject': continue
        try: go=o.read_typetree()
        except Exception: continue
        comps=[tt(c['component']['m_PathID']) for c in go['m_Component']]
        tr=[c for c in comps if c[0]=='Transform']
        if not tr or tr[0][1]['m_Father']['m_PathID']!=0: continue
        piece=[c[1] for c in comps if c[0]=='MonoBehaviour' and 'm_category' in c[1] and 'm_resources' in c[1] and 'm_craftingStation' in c[1]]
        if not piece: continue
        pts=[]
        def walk(gopid,pos,rot,scale,root):
            t,g=tt(gopid)
            if g is None or g.get('m_IsActive',1)!=1: return
            trc=None; boxes=[]
            for c in g['m_Component']:
                ct,cm=tt(c['component']['m_PathID'])
                if ct=='Transform': trc=cm
                elif ct=='BoxCollider' and cm.get('m_Enabled',1)==1 and not cm.get('m_IsTrigger',0): boxes.append(cm)
            if trc is None: return
            if root: p,r,s=(0,0,0),(0,0,0,1),(1,1,1)
            else:
                lp=trc['m_LocalPosition']; lr=trc['m_LocalRotation']; ls=trc['m_LocalScale']
                p=qrot(rot,(lp['x']*scale[0],lp['y']*scale[1],lp['z']*scale[2])); p=(p[0]+pos[0],p[1]+pos[1],p[2]+pos[2])
                r=qmul(rot,(lr['x'],lr['y'],lr['z'],lr['w'])); s=(scale[0]*ls['x'],scale[1]*ls['y'],scale[2]*ls['z'])
            for b in boxes:
                c=b['m_Center']; z=b['m_Size']
                for dx in (-0.5,0.5):
                    for dy in (-0.5,0.5):
                        for dz in (-0.5,0.5):
                            v=((c['x']+dx*z['x'])*s[0],(c['y']+dy*z['y'])*s[1],(c['z']+dz*z['z'])*s[2]); v=qrot(r,v)
                            pts.append((v[0]+p[0],v[1]+p[1],v[2]+p[2]))
            for ch in trc['m_Children']:
                ct,cm=tt(ch['m_PathID'])
                if cm: walk(cm['m_GameObject']['m_PathID'],p,r,s,False)
        walk(o.path_id,None,None,None,True)
        pc=piece[0]
        entry={'cat':pc['m_category'],'comfort':pc.get('m_comfort',0)}
        if pts:
            xs=[q[0] for q in pts]; ys=[q[1] for q in pts]; zs=[q[2] for q in pts]
            entry['box']=[round(min(xs),3),round(max(xs),3),round(min(zs),3),round(max(zs),3),round(min(ys),3),round(max(ys),3)]
        out[go['m_Name']]=entry
    if fi%60==0: print('scanned',fi,'/',len(files),'pieces',len(out),file=sys.stderr)
json.dump(out,open(sys.argv[1],'w'))
print('pieces:',len(out),'with footprint:',sum(1 for v in out.values() if 'box' in v))
for n in ['wood_floor','wood_wall_half','stone_wall_2x1','piece_workbench','wood_door','woodwall','wood_roof','portal_wood','fire_pit']:
    print(n,out.get(n))
