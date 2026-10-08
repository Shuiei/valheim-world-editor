# The cave roof rocks' shapes for the editor (Core/WorldGen/cave-rocks.json): each prefab's mesh in its own
# frame (the model's scale applied), as vertices and triangles. Usage: scan_cave_rocks.py <export-cache/scan.json> <bundles> <out.json>
import UnityPy, json, sys, os
scan = json.load(open(sys.argv[1])); B = sys.argv[2]
out = {}
for n, child_scale in (('rock4_forest', 4.0), ('rock3_mountain', 4.0)):
    bundle, pid = scan['roots'][n][0], scan['roots'][n][1]
    env = UnityPy.load(os.path.join(B, bundle)); byid = {o.path_id: o for o in env.objects}
    mesh = next(o for o in env.objects if o.type.name == 'Mesh' and o.read().m_Name == ('Rock4' if n.startswith('rock4') else 'Rock3'))
    obj = mesh.read().export()
    vs, tris = [], []
    for line in obj.splitlines():
        p = line.split()
        if not p: continue
        if p[0] == 'v':
            # UnityPy's OBJ export mirrors x (OBJ's handedness): back to Unity's.
            vs.append([round(-float(p[1]) * child_scale, 3), round(float(p[2]) * child_scale, 3), round(float(p[3]) * child_scale, 3)])
        elif p[0] == 'f':
            idx = [int(q.split('/')[0]) - 1 for q in p[1:]]
            for k in range(1, len(idx) - 1):
                tris += [idx[0], idx[k], idx[k + 1]]
    ys = [v[1] for v in vs]; xs = [v[0] for v in vs]; zs = [v[2] for v in vs]
    print(n, len(vs), 'verts', len(tris) // 3, 'tris', 'x', min(xs), max(xs), 'y', min(ys), max(ys), 'z', min(zs), max(zs))
    out[n] = {'v': vs, 't': tris}
json.dump(out, open(sys.argv[3], 'w'), separators=(',', ':'))
