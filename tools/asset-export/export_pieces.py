# Export Valheim build-piece models for the terrain editor (three.js space: z mirrored).
# out/pieces/<name>.json: {parts:[{mesh, sub, mat, m:[16 col-major]}]}
# out/meshes/<id>.bin: float32 pos*3, float32 normal*3, float32 uv*2 per vertex, then uint32 indices per submesh
# out/meshes/<id>.json-ish info is folded into the piece json (vertex count, submesh index ranges)
# out/tex/<id>.(jpg|png), materials in out/materials.json
import sys, os, json, math, struct, hashlib, traceback
sys.path.insert(0, os.path.dirname(__file__))
import assetlib, UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from PIL import Image

OUT = sys.argv[1]
ONLY = set(sys.argv[2].split(',')) if len(sys.argv) > 2 and sys.argv[2] else None
TEXMAX = 1024
for d in ('pieces', 'meshes', 'tex'): os.makedirs(os.path.join(OUT, d), exist_ok=True)
index = json.load(open(os.environ.get('INDEX') or os.path.join(os.path.dirname(__file__), 'piece_index.json')))
# object_index.json format: {hash: {name, bundle, pid}} -> {name: [bundle, pid]}
if index and isinstance(next(iter(index.values())), dict): index = {v['name']: [v['bundle'], v['pid']] for v in index.values()}
matfile = os.path.join(OUT, 'materials.json')
materials = json.load(open(matfile)) if os.path.exists(matfile) else {}
mesh_info_file = os.path.join(OUT, 'meshinfo.json')
mesh_info = json.load(open(mesh_info_file)) if os.path.exists(mesh_info_file) else {}
# A mesh counts as done only once meshinfo.json lists it (a run stopped in between made it again).
done_meshes = set(os.path.splitext(f)[0] for f in os.listdir(os.path.join(OUT, 'meshes')) if f.endswith('.bin')) & set(mesh_info)
done_tex = {}

# Every file is written whole or not at all (a temporary file, then renamed): a run stopped halfway
# (the editor closed) leaves no cut file that the next run would take as done.
def write_file(path, data, mode='wb'):
    tmp = path + '.tmp'
    with open(tmp, mode) as f: f.write(data)
    os.replace(tmp, path)

def save_image(img, path, **kw):
    tmp = path + '.tmp'
    img.save(tmp, format='PNG' if path.endswith('.png') else 'JPEG', **kw)
    os.replace(tmp, path)

# The pieces exported since materials.json and meshinfo.json were last written: their files are
# renamed into place only after (a piece whose materials are not saved yet is not done).
pending = []

def save_progress():
    write_file(matfile, json.dumps(materials), 'w'); write_file(mesh_info_file, json.dumps(mesh_info), 'w')
    for tmp in pending: os.replace(tmp, tmp[:-len('.part')])
    pending.clear()

def mat4_trs(p, q, s):
    x, y, z, w = q
    r = [[1 - 2*(y*y + z*z), 2*(x*y - z*w), 2*(x*z + y*w)],
         [2*(x*y + z*w), 1 - 2*(x*x + z*z), 2*(y*z - x*w)],
         [2*(x*z - y*w), 2*(y*z + x*w), 1 - 2*(x*x + y*y)]]
    return [[r[0][0]*s[0], r[0][1]*s[1], r[0][2]*s[2], p[0]],
            [r[1][0]*s[0], r[1][1]*s[1], r[1][2]*s[2], p[1]],
            [r[2][0]*s[0], r[2][1]*s[1], r[2][2]*s[2], p[2]],
            [0, 0, 0, 1]]

def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

F = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, -1, 0], [0, 0, 0, 1]]

def key_of(obj):
    return f"{obj.assets_file.name}:{obj.path_id}"

def mesh_id(obj):
    return hashlib.sha1(key_of(obj).encode()).hexdigest()[:16]

def export_mesh(obj):
    mid = mesh_id(obj)
    if mid in done_meshes and mid in mesh_info: return mid
    m = obj.read()
    h = MeshHandler(m); h.process()
    n = h.m_VertexCount
    pos = h.m_Vertices; nor = h.m_Normals or [(0, 1, 0)] * n; uv = h.m_UV0 or [(0, 0)] * n
    subs = h.get_triangles()
    buf = bytearray()
    for i in range(n):
        p = pos[i]; q = nor[i]; t = uv[i]
        buf += struct.pack('<8f', p[0], p[1], -p[2], q[0], q[1], -q[2], t[0], t[1])
    ranges = []
    for tri in subs:
        start = len(buf)
        flat = [i for t in tri for i in t]
        buf += struct.pack(f'<{len(flat)}I', *flat)
        ranges.append(len(flat))
    write_file(os.path.join(OUT, 'meshes', mid + '.bin'), buf)
    mesh_info[mid] = {'v': n, 'sub': ranges, 'name': m.m_Name}
    done_meshes.add(mid)
    return mid

def export_tex(owner, ref):
    t = assetlib.resolve(owner, ref)
    if t is None: return None
    k = key_of(t)
    if k in done_tex: return done_tex[k]
    tid = hashlib.sha1(k.encode()).hexdigest()[:16]
    for ext in ('jpg', 'png'):
        if os.path.exists(os.path.join(OUT, 'tex', f'{tid}.{ext}')):
            done_tex[k] = f'{tid}.{ext}'; return done_tex[k]
    try:
        td = t.read(); img = td.image
    except Exception as ex:
        print('  texture failed', ex, file=sys.stderr); done_tex[k] = None; return None
    if max(img.size) > TEXMAX:
        f = TEXMAX / max(img.size)
        img = img.resize((max(1, int(img.width * f)), max(1, int(img.height * f))), Image.LANCZOS)
    img = img.transpose(Image.FLIP_TOP_BOTTOM)  # Unity origin is bottom-left; three uses flipY=false
    if img.mode == 'RGBA' and img.getchannel('A').getextrema()[0] < 250:
        name = f'{tid}.png'; save_image(img, os.path.join(OUT, 'tex', name), optimize=True)
    else:
        name = f'{tid}.jpg'; save_image(img.convert('RGB'), os.path.join(OUT, 'tex', name), quality=88)
    done_tex[k] = name
    return name

def export_material(obj):
    k = hashlib.sha1(key_of(obj).encode()).hexdigest()[:16]
    if k in materials: return k
    mt = obj.read_typetree()
    sh = assetlib.resolve(obj, mt['m_Shader'])
    shname = ''
    if sh is not None:
        try: shname = sh.read_typetree()['m_ParsedForm']['m_Name']
        except Exception: pass
    props = mt['m_SavedProperties']
    texs = {n: t for n, t in props['m_TexEnvs']}
    floats = {n: v for n, v in props['m_Floats']}
    colors = {n: v for n, v in props['m_Colors']}
    entry = {'name': mt['m_Name'], 'shader': shname}
    for prop, key in (('_MainTex', 'map'), ('_BumpMap', 'normal')):
        if prop in texs and texs[prop]['m_Texture']['m_PathID']:
            name = export_tex(obj, texs[prop]['m_Texture'])
            if name:
                entry[key] = name
                sc = texs[prop]['m_Scale']; off = texs[prop]['m_Offset']
                if key == 'map' and (sc['x'], sc['y'], off['x'], off['y']) != (1, 1, 0, 0): entry['uv'] = [sc['x'], sc['y'], off['x'], off['y']]
    c = colors.get('_Color')
    if c: entry['color'] = [round(c['r'], 4), round(c['g'], 4), round(c['b'], 4), round(c['a'], 4)]
    # Unity Standard-style blend mode: 1 = cutout. Vegetation shaders always cut out.
    if floats.get('_Mode') == 1 or 'Vegetation' in shname or 'Grass' in shname: entry['cutoff'] = round(floats.get('_Cutoff', 0.5) or 0.5, 3)
    if floats.get('_Cull', 2) == 0 or 'Vegetation' in shname: entry['doubleSided'] = True
    if floats.get('_Mode') in (2, 3) or 'transparent' in shname.lower(): entry['transparent'] = True
    for f in ('_Glossiness', '_Metallic'):
        if f in floats: entry[f[1:].lower()] = round(floats[f], 3)
    materials[k] = entry
    return k

def comps(byid, go):
    out = []
    for c in go['m_Component']:
        o = byid.get(c['component']['m_PathID'])
        if o is None: continue
        try: out.append((o.type.name, o, o.read_typetree()))
        except Exception: pass
    return out

def export_piece(name, bundle, pid):
    env, byid = assetlib.env_for_bundle(bundle)
    root = byid[pid]
    skip_go = set(); lod_skip = set()
    # First pass: WearNTear variants and lower LODs to leave out.
    def scan(gopid):
        go = byid[gopid].read_typetree()
        for t, o, tt in comps(byid, go):
            if t == 'MonoBehaviour':
                for f in ('m_worn', 'm_broken', 'm_wet'):
                    r = tt.get(f)
                    if isinstance(r, dict) and r.get('m_PathID') and r.get('m_FileID') == 0 and (f != 'm_wet'): skip_go.add(r['m_PathID'])
                if isinstance(tt.get('m_new'), dict):
                    nr = tt['m_new'].get('m_PathID')
                    for f in ('m_worn', 'm_broken'):
                        r = tt.get(f)
                        if isinstance(r, dict) and r.get('m_PathID') == nr: skip_go.discard(nr)
            if t == 'LODGroup':
                for li, lod in enumerate(tt['m_LODs']):
                    if li == 0: continue
                    for r in lod['renderers']:
                        if r['renderer']['m_PathID']: lod_skip.add(r['renderer']['m_PathID'])
                for r in tt['m_LODs'][0]['renderers'] if tt['m_LODs'] else []:
                    lod_skip.discard(r['renderer']['m_PathID'])
            if t == 'Transform':
                for ch in tt['m_Children']:
                    co = byid.get(ch['m_PathID'])
                    if co: scan(co.read_typetree()['m_GameObject']['m_PathID'])
    scan(pid)
    parts = []
    def walk(gopid, M, isroot):
        if gopid in skip_go and not isroot: return
        go = byid[gopid].read_typetree()
        if not isroot and go.get('m_IsActive', 1) != 1: return
        cs = comps(byid, go)
        tr = next((tt for t, o, tt in cs if t == 'Transform'), None)
        if tr is None: return
        if isroot: W = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
        else:
            lp, lr, ls = tr['m_LocalPosition'], tr['m_LocalRotation'], tr['m_LocalScale']
            W = mul(M, mat4_trs((lp['x'], lp['y'], lp['z']), (lr['x'], lr['y'], lr['z'], lr['w']), (ls['x'], ls['y'], ls['z'])))
        mf = next(((o, tt) for t, o, tt in cs if t == 'MeshFilter'), None)
        mr = next(((o, tt) for t, o, tt in cs if t == 'MeshRenderer'), None)
        # Skinned meshes (animated stations) are drawn in their bind pose at the renderer's transform.
        smr = next(((o, tt) for t, o, tt in cs if t == 'SkinnedMeshRenderer'), None)
        if smr and not mr: mf, mr = (smr[0], {'m_Mesh': smr[1]['m_Mesh']}), smr
        if mf and mr and mr[1].get('m_Enabled', 1) == 1 and mr[0].path_id not in lod_skip:
            mo = assetlib.resolve(mf[0], mf[1]['m_Mesh'])
            if mo is not None:
                try:
                    mid = export_mesh(mo)
                    T = mul(mul(F, W), F)
                    flatm = [T[r][c] for c in range(4) for r in range(4)]
                    for si, mref in enumerate(mr[1]['m_Materials']):
                        if si >= len(mesh_info[mid]['sub']): break
                        mo2 = assetlib.resolve(mr[0], mref)
                        mk = export_material(mo2) if mo2 is not None else None
                        parts.append({'mesh': mid, 'sub': si, 'mat': mk, 'm': [round(v, 5) for v in flatm]})
                except Exception as ex:
                    print('  mesh failed', name, ex, file=sys.stderr)
        for ch in tr['m_Children']:
            co = byid.get(ch['m_PathID'])
            if co: walk(co.read_typetree()['m_GameObject']['m_PathID'], W, False)
    walk(pid, None, True)
    rt = next((tt for t, o, tt in comps(byid, byid[pid].read_typetree()) if t == 'Transform'), None)
    rs = rt['m_LocalScale'] if rt else {'x': 1, 'y': 1, 'z': 1}
    part = os.path.join(OUT, 'pieces', name + '.json.part')
    write_file(part, json.dumps({'parts': parts, 'rootScale': [rs['x'], rs['y'], rs['z']]}), 'w')
    pending.append(part)
    return len(parts)

names = sorted(index)
if ONLY: names = [n for n in names if n in ONLY]
# Group by bundle to keep few bundles loaded at once.
names.sort(key=lambda n: index[n][0])
cur = None
for i, n in enumerate(names):
    b, pid = index[n]
    if b != cur:
        # Free bundles we no longer need (keep shared texture/material bundles cached by assetlib anyway).
        if len(assetlib._envs) > 40: assetlib._envs.clear()
        cur = b
    try:
        k = export_piece(n, b, pid)
        print(f'{i+1}/{len(names)} {n}: {k} parts', flush=True)
    except Exception:
        print('FAILED', n, file=sys.stderr); traceback.print_exc()
    if i % 25 == 0: save_progress()
save_progress()
