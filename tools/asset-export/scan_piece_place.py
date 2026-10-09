# How the game's hammer places each piece (Core/WorldGen/piece-place.json, in git), for the Workshop's
# Build to place and snap pieces as the game does (Player.UpdatePlacementGhost, FindClosestSnapPoints,
# Piece.GetSnapPoints): the piece's placement flags (m_groundPiece, m_clipGround, m_clipEverything,
# m_allowRotatedOverlap, m_waterPiece, m_noInWater), its snap points (its direct children tagged
# "snappoint"), and its colliders the build ray and the ghost use: enabled, not triggers, on an active
# object, on a layer of the build ray's mask (Default, static_solid, Default_small, piece,
# piece_nonsolid, terrain, vehicle). Everything in the piece's own frame (its root at the origin, its
# root scale applied). Colliders: ["b", cx,cy,cz, hx,hy,hz, qx,qy,qz,qw] a box; ["s", cx,cy,cz, r] a
# sphere; ["c", ax,ay,az, bx,by,bz, r] a capsule (its segment); ["m", convex, [x,y,z,...], [i,j,k,...]] a
# mesh (triangles).
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_piece_place.py <out.json>
import UnityPy, glob, json, sys
import os as _os
from UnityPy.helpers.MeshHelper import MeshHandler
B = _os.path.join(_os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
RAY_LAYERS = {0, 15, 20, 10, 16, 11, 28}


def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw*bx+ax*bw+ay*bz-az*by, aw*by-ax*bz+ay*bw+az*bx, aw*bz+ax*by-ay*bx+az*bw, aw*bw-ax*bx-ay*by-az*bz)


def qrot(q, v):
    x, y, z, w = q; vx, vy, vz = v
    ix = w*vx + y*vz - z*vy; iy = w*vy + z*vx - x*vz; iz = w*vz + x*vy - y*vx; iw = -x*vx - y*vy - z*vz
    return (ix*w + iw*-x + iy*-z - iz*-y, iy*w + iw*-y + iz*-x - ix*-z, iz*w + iw*-z + ix*-y - iy*-x)


def r(v):
    return round(v, 4)


out = {}
mismatched = 0
for f in sorted(glob.glob(B + '*')):
    try:
        env = UnityPy.load(f)
    except Exception:
        continue
    byid = {o.path_id: o for o in env.objects}
    cache, meshes = {}, {}

    def tt(pid):
        if pid not in cache:
            o = byid.get(pid)
            try:
                cache[pid] = (o.type.name, o.read_typetree()) if o else (None, None)
            except Exception:
                cache[pid] = (None, None)
        return cache[pid]

    def mesh_of(pid):
        global mismatched
        if pid not in meshes:
            o = byid.get(pid)
            meshes[pid] = None
            if o is not None:
                try:
                    m = o.read()
                    h = MeshHandler(m); h.process()
                    v = [c for p in h.m_Vertices for c in p]
                    idx = [i for sub in h.get_triangles() for t in sub for i in t]
                    a = m.m_LocalAABB
                    xs = v[0::3]
                    if xs and abs((min(xs) + max(xs)) / 2 - a.m_Center.x) > 0.02:
                        mismatched += 1
                    meshes[pid] = (v, idx)
                except Exception:
                    pass
        return meshes[pid]

    for o in env.objects:
        if o.type.name != 'GameObject':
            continue
        try:
            go = o.read_typetree()
        except Exception:
            continue
        comps = [tt(c['component']['m_PathID']) for c in go['m_Component']]
        tr = [c for c in comps if c[0] == 'Transform']
        if not tr or tr[0][1]['m_Father']['m_PathID'] != 0:
            continue
        piece = [c[1] for c in comps if c[0] == 'MonoBehaviour' and 'm_category' in c[1] and 'm_resources' in c[1] and 'm_craftingStation' in c[1]]
        if not piece:
            continue
        p = piece[0]
        root = tr[0][1]
        rs = root['m_LocalScale']; rs = (rs['x'], rs['y'], rs['z'])
        snaps, cols = [], []
        # Snap points: the root's direct children with the "snappoint" tag (20000), active or not.
        for ch in root['m_Children']:
            ct, cm = tt(ch['m_PathID'])
            if not cm:
                continue
            _, g = tt(cm['m_GameObject']['m_PathID'])
            if g and g.get('m_Tag') == 20000:
                lp = cm['m_LocalPosition']
                snaps.append([r(lp['x']*rs[0]), r(lp['y']*rs[1]), r(lp['z']*rs[2])])

        def walk(gopid, pos, rot, scale, isroot):
            t, g = tt(gopid)
            if g is None or not g.get('m_IsActive', 1):
                return
            trc, colliders = None, []
            for c in g['m_Component']:
                ct, cm = tt(c['component']['m_PathID'])
                if ct == 'Transform':
                    trc = cm
                elif ct in ('BoxCollider', 'MeshCollider', 'SphereCollider', 'CapsuleCollider') and cm and not cm.get('m_IsTrigger', 0) and cm.get('m_Enabled', 1) == 1:
                    colliders.append((ct, cm))
            if trc is None:
                return
            if isroot:
                pp, q, s = (0, 0, 0), (0, 0, 0, 1), rs
            else:
                lp = trc['m_LocalPosition']; lr = trc['m_LocalRotation']; ls = trc['m_LocalScale']
                pp = qrot(rot, (lp['x']*scale[0], lp['y']*scale[1], lp['z']*scale[2])); pp = (pp[0]+pos[0], pp[1]+pos[1], pp[2]+pos[2])
                q = qmul(rot, (lr['x'], lr['y'], lr['z'], lr['w'])); s = (scale[0]*ls['x'], scale[1]*ls['y'], scale[2]*ls['z'])
            if g.get('m_Layer', 0) in RAY_LAYERS:
                def P(v):
                    w = qrot(q, (v[0]*s[0], v[1]*s[1], v[2]*s[2]))
                    return (w[0]+pp[0], w[1]+pp[1], w[2]+pp[2])
                for ct, cm in colliders:
                    if ct == 'BoxCollider':
                        c, z = cm['m_Center'], cm['m_Size']
                        cc = P((c['x'], c['y'], c['z']))
                        cols.append(['b', r(cc[0]), r(cc[1]), r(cc[2]), r(abs(z['x']*s[0])/2), r(abs(z['y']*s[1])/2), r(abs(z['z']*s[2])/2), r(q[0]), r(q[1]), r(q[2]), r(q[3])])
                    elif ct == 'SphereCollider':
                        c = cm['m_Center']; cc = P((c['x'], c['y'], c['z']))
                        cols.append(['s', r(cc[0]), r(cc[1]), r(cc[2]), r(cm['m_Radius'] * max(abs(x) for x in s))])
                    elif ct == 'CapsuleCollider':
                        c = cm['m_Center']; d = cm.get('m_Direction', 1); rr = cm['m_Radius'] * max(abs(s[i]) for i in range(3) if i != d)
                        half = max(cm['m_Height'] * abs(s[d]) / 2 - rr, 0)
                        ax = [0, 0, 0]; ax[d] = half / abs(s[d]) if s[d] else 0
                        a = P((c['x'] + ax[0], c['y'] + ax[1], c['z'] + ax[2])); b = P((c['x'] - ax[0], c['y'] - ax[1], c['z'] - ax[2]))
                        cols.append(['c', r(a[0]), r(a[1]), r(a[2]), r(b[0]), r(b[1]), r(b[2]), r(rr)])
                    else:
                        m = cm.get('m_Mesh', {})
                        md = mesh_of(m.get('m_PathID')) if m.get('m_FileID', 0) == 0 else None
                        if md:
                            v, idx = md
                            pts = [P((v[i], v[i+1], v[i+2])) for i in range(0, len(v), 3)]
                            cols.append(['m', int(bool(cm.get('m_Convex', 0))), [r(c) for p3 in pts for c in p3], idx])
            for ch in trc['m_Children']:
                ct, cm = tt(ch['m_PathID'])
                if cm:
                    walk(cm['m_GameObject']['m_PathID'], pp, q, s, False)

        walk(o.path_id, None, None, None, True)
        flags = {k: int(bool(p.get(f, 0))) for k, f in (('g', 'm_groundPiece'), ('cg', 'm_clipGround'), ('ce', 'm_clipEverything'), ('ro', 'm_allowRotatedOverlap'), ('w', 'm_waterPiece'), ('nw', 'm_noInWater'))}
        out[go['m_Name']] = {'f': {k: v for k, v in flags.items() if v}, 's': snaps, 'c': cols}
json.dump(out, open(sys.argv[1], 'w'), separators=(',', ':'))
print('pieces:', len(out), 'meshes whose vertices do not match their box:', mismatched)
print('collider kinds:', {k: sum(1 for v in out.values() for c in v['c'] if c[0] == k) for k in 'bscm'}, 'convex meshes:', sum(1 for v in out.values() for c in v['c'] if c[0] == 'm' and c[1]))
for n in ['woodwall', 'wood_roof', 'stone_wall_2x1', 'wood_floor', 'piece_workbench']:
    v = out.get(n)
    print(n, v and {'f': v['f'], 's': v['s'][:4], 'c': [c[:8] if c[0] != 'm' else ['m', c[1], len(c[2]) // 3, len(c[3]) // 3] for c in v['c']]})
