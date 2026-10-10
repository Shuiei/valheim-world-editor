# What the game's support check (WearNTear.UpdateSupport) needs of every build piece, for the
# editor's stability check (Core/WorldGen/piece-support.json, in git): its material (WearNTear.
# m_materialType: wood, stone, iron...), centre of mass offset, whether it supports other pieces,
# whether the game checks its support at all (f: never), and its colliders as boxes in the piece's own
# frame. The game tests support with each collider's box, grown by 0.3 m: a BoxCollider's own turned
# box, any other collider's axis-aligned bounds (a mesh collider's are kept here as its mesh's box,
# turned with the piece at run time). Colliders of every
# child count, inactive ones too (GetComponentsInChildren(includeInactive: true)); triggers do not.
# Each box: [cx, cy, cz, hx, hy, hz, qx, qy, qz, qw, aabb, layer] (centre, half size, rotation; aabb 1:
# bounds of a non-box collider; the collider's layer).
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_piece_support.py <out.json>
import UnityPy, glob, json, sys
import os as _os
B = _os.path.join(_os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')


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
files = sorted(glob.glob(B + '*'))
for fi, f in enumerate(files):
    try:
        env = UnityPy.load(f)
    except Exception:
        continue
    byid = {o.path_id: o for o in env.objects}
    cache = {}

    def tt(pid):
        if pid not in cache:
            o = byid.get(pid)
            try:
                cache[pid] = (o.type.name, o.read_typetree()) if o else (None, None)
            except Exception:
                cache[pid] = (None, None)
        return cache[pid]

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
        if not any(c[0] == 'MonoBehaviour' and 'm_category' in c[1] and 'm_resources' in c[1] for c in comps):
            continue
        wnt = [c[1] for c in comps if c[0] == 'MonoBehaviour' and 'm_materialType' in c[1] and 'm_supports' in c[1]]
        if not wnt:
            continue
        boxes = []

        def walk(gopid, pos, rot, scale, root):
            t, g = tt(gopid)
            if g is None:
                return
            # Snap points carry no collider that counts; other inactive children do (the game asks for them).
            if g.get('m_Tag') == 20000 or 'snappoint' in g.get('m_Name', '').lower():
                return
            layer = g.get('m_Layer', 0)
            trc = None
            cols = []
            for c in g['m_Component']:
                ct, cm = tt(c['component']['m_PathID'])
                if ct == 'Transform':
                    trc = cm
                elif ct in ('BoxCollider', 'MeshCollider', 'SphereCollider', 'CapsuleCollider') and cm and not cm.get('m_IsTrigger', 0) and cm.get('m_Enabled', 1) == 1:
                    cols.append((ct, cm))
            if trc is None:
                return
            if root:
                p, q, s = (0, 0, 0), (0, 0, 0, 1), (1, 1, 1)
            else:
                lp = trc['m_LocalPosition']; lr = trc['m_LocalRotation']; ls = trc['m_LocalScale']
                p = qrot(rot, (lp['x']*scale[0], lp['y']*scale[1], lp['z']*scale[2])); p = (p[0]+pos[0], p[1]+pos[1], p[2]+pos[2])
                q = qmul(rot, (lr['x'], lr['y'], lr['z'], lr['w'])); s = (scale[0]*ls['x'], scale[1]*ls['y'], scale[2]*ls['z'])
            for ct, cm in cols:
                if ct == 'BoxCollider':
                    c, z, aabb = cm['m_Center'], cm['m_Size'], 0
                    c = (c['x'], c['y'], c['z']); h = (z['x'] / 2, z['y'] / 2, z['z'] / 2)
                elif ct == 'MeshCollider':
                    mesh = cm.get('m_Mesh', {})
                    mt = tt(mesh.get('m_PathID')) if mesh.get('m_FileID', 0) == 0 else (None, None)
                    if mt[1] is None:
                        continue
                    a = mt[1]['m_LocalAABB']
                    c = (a['m_Center']['x'], a['m_Center']['y'], a['m_Center']['z']); h = (a['m_Extent']['x'], a['m_Extent']['y'], a['m_Extent']['z']); aabb = 1
                elif ct == 'SphereCollider':
                    c = (cm['m_Center']['x'], cm['m_Center']['y'], cm['m_Center']['z']); rr = cm['m_Radius']; h = (rr, rr, rr); aabb = 1
                else:
                    c = (cm['m_Center']['x'], cm['m_Center']['y'], cm['m_Center']['z']); rr = cm['m_Radius']; hh = max(cm['m_Height'] / 2, rr)
                    d = cm.get('m_Direction', 1)
                    h = tuple(hh if i == d else rr for i in range(3)); aabb = 1
                cs = qrot(q, (c[0]*s[0], c[1]*s[1], c[2]*s[2]))
                boxes.append([r(cs[0]+p[0]), r(cs[1]+p[1]), r(cs[2]+p[2]), r(abs(h[0]*s[0])), r(abs(h[1]*s[1])), r(abs(h[2]*s[2])), r(q[0]), r(q[1]), r(q[2]), r(q[3]), aabb, layer])
            for ch in trc['m_Children']:
                ct, cm = tt(ch['m_PathID'])
                if cm:
                    walk(cm['m_GameObject']['m_PathID'], p, q, s, False)

        walk(o.path_id, None, None, None, True)
        w = wnt[0]
        com = w.get('m_comOffset', {'x': 0, 'y': 0, 'z': 0})
        out[go['m_Name']] = {'m': w['m_materialType'], 's': int(w.get('m_supports', 1)), 'c': [r(com['x']), r(com['y']), r(com['z'])], 'b': boxes}
        # f: the game never checks its support (m_noSupportWear off): it never falls, and keeps its full
        # support for what rests on it (black marble's large floor, some dungeon pieces).
        if not w.get('m_noSupportWear', 1):
            out[go['m_Name']]['f'] = 1
    if fi % 60 == 0:
        print('scanned', fi, '/', len(files), 'pieces', len(out), file=sys.stderr)
json.dump(out, open(sys.argv[1], 'w'), separators=(',', ':'))
print('pieces with support data:', len(out), 'without colliders:', sum(1 for v in out.values() if not v['b']))
for n in ['wood_floor', 'woodwall', 'stone_wall_2x1', 'wood_pole2', 'wood_beam', 'wood_roof', 'piece_workbench']:
    print(n, json.dumps(out.get(n))[:300])
