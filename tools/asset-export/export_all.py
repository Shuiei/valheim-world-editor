#!/usr/bin/env python3
"""Extract everything the editor's in-game look needs from a Valheim install, into the editor's game-look folder.

    python export_all.py --valheim "/path/to/Valheim" --out ~/.local/share/ValheimWorldEditor/game-look

Writes, under --out:
  terrain/heightmap.frag.glsl      the game's terrain shader (Custom/Heightmap, deferred pass) for WebGL 2
                                   (heightmap.frag.spv instead, from a copy without OpenGL shaders: Windows)
  terrain/*.png                    terrain textures (texture arrays stacked into vertical strips)
  maptex/*.png                     world map textures
  models/                          models of build pieces and world objects (meshes, textures, materials)

These files belong to the game: they are made from your own copy and must not be redistributed.

Needs Python 3 with: pip install -r requirements.txt (UnityPy 1.25, Pillow, lz4, numpy).
The first step reads every asset bundle of the game once (a minute or two) and keeps the result in
--work, so later runs skip it. Model export is incremental: an interrupted run continues.
A full run takes a few minutes and writes about 150 MB.

--objects chooses which world objects get models (build pieces always do):
  all      every placeable kind in the game (default; the longest)
  world    only kinds found in the save given with --world (a world folder with *.chunk files)
  none     build pieces only
"""
import argparse, collections, glob, json, os, re, struct, subprocess, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))


def log(*a):
    print(time.strftime('%H:%M:%S'), *a, flush=True)


def stable_hash(s):
    # Valheim's String.GetStableHashCode (prefab ids).
    a = b = 5381
    for i in range(0, len(s), 2):
        a = ((a << 5) + a ^ ord(s[i])) & 0xffffffff
        if i == len(s) - 1: break
        b = ((b << 5) + b ^ ord(s[i + 1])) & 0xffffffff
    r = (a + b * 1566083941) & 0xffffffff
    return r - (1 << 32) if r >= 1 << 31 else r


def find_bundles(valheim):
    for sub in ('valheim_Data', 'Valheim_Data', 'valheim.app/Contents/Resources/Data'):
        d = os.path.join(valheim, sub, 'StreamingAssets', 'SoftRef', 'Bundles')
        if os.path.isdir(d): return d
    sys.exit(f'No asset bundles under {valheim} (expected valheim_Data/StreamingAssets/SoftRef/Bundles). '
             'Point --valheim at the game folder (the client: a dedicated server has no models).')


# ---- 1. One pass over every bundle: where things are.
def scan(bundles, work):
    path = os.path.join(work, 'scan.json')
    if os.path.exists(path):
        log('scan: using', path)
        return json.load(open(path))
    import UnityPy
    res = {'cab': {}, 'roots': {}, 'heightmap': None, 'arrays': {}, 'minimap': None}
    files = sorted(f for f in glob.glob(os.path.join(bundles, '*')) if os.path.isfile(f))
    log(f'scan: reading {len(files)} bundles (once)')
    for fi, f in enumerate(files):
        name = os.path.basename(f)
        try: env = UnityPy.load(f)
        except Exception: continue
        for bf in env.files.values():
            for sub in getattr(bf, 'files', {}).keys():
                res['cab'][sub.split('.')[0].lower()] = name
        byid = {o.path_id: o for o in env.objects}
        for o in env.objects:
            t = o.type.name
            if t == 'Texture2DArray':
                try: n = o.peek_name()
                except Exception: n = None
                if n in ('terrain_d_array', 'terrain_n_array'): res['arrays'][n] = name
            elif t == 'Material' and res['minimap'] is None:
                try: n = o.peek_name()
                except Exception: n = None
                if n == 'minimap': res['minimap'] = [name, o.path_id]
            elif t == 'GameObject':
                try: go = o.read_typetree()
                except Exception: continue
                root = False; znv = None; creature = item = False
                for c in go['m_Component']:
                    co = byid.get(c['component']['m_PathID'])
                    if co is None: continue
                    try: tt = co.read_typetree()
                    except Exception: continue
                    if co.type.name == 'Transform': root = tt['m_Father']['m_PathID'] == 0
                    elif co.type.name == 'MonoBehaviour':
                        if 'm_persistent' in tt and 'm_distant' in tt and 'm_type' in tt: znv = int(tt['m_persistent'])
                        if 'm_runSpeed' in tt: creature = True
                        if 'm_itemData' in tt: item = True
                        if res['heightmap'] is None and 'm_isDistantLod' in tt and not tt['m_isDistantLod'] and 'm_material' in tt:
                            res['heightmap'] = [name, co.path_id]
                if not root: continue
                n = go['m_Name']
                prev = res['roots'].get(n)
                # Several roots can share a name (a model asset and the prefab made from it): prefer the
                # one with a ZNetView, the real prefab.
                if prev is None or (prev[2] is None and znv is not None):
                    res['roots'][n] = [name, o.path_id, znv, int(creature), int(item)]
        if fi % 50 == 0: log(f'scan: {fi}/{len(files)} bundles, {len(res["roots"])} root objects')
    os.makedirs(work, exist_ok=True)
    json.dump(res, open(path, 'w'))
    json.dump(res['cab'], open(os.path.join(work, 'cab_index.json'), 'w'))
    log('scan: done')
    return res


def use_assetlib(bundles, work):
    os.environ['VWE_BUNDLES'] = bundles
    os.environ['VWE_CAB_INDEX'] = os.path.join(work, 'cab_index.json')
    if HERE not in sys.path: sys.path.insert(0, HERE)
    import assetlib
    return assetlib


def flat(x):
    while isinstance(x, list) and x and isinstance(x[0], list): x = [i for s in x for i in s]
    return x


# ---- 2. Terrain: the Custom/Heightmap shader and its textures.
TERRAIN_TEX = {'_CliffNormal': 'cliff_n', '_ColorVarietyNoise': 'variety', '_MistlandsCliffNormal': 'mistcliff_n',
               '_NoiseTex': 'noise', '_PavedNormal': 'paved_n', '_RockNormal': 'rock_n', '_SnowNormal': 'snow_n'}
# Texture2DArray stores a GraphicsFormat; map the compressed ones to TextureFormat.
GF = {96: 10, 97: 10, 98: 11, 99: 11, 100: 12, 101: 12, 102: 26, 103: 26, 104: 27, 105: 27, 106: 24, 107: 24, 108: 25, 109: 25, 8: 4, 4: 4}


def export_terrain(found, al, out, vulkan=False):
    import lz4.block, UnityPy
    from PIL import Image
    from UnityPy.export import Texture2DConverter
    tdir = os.path.join(out, 'terrain'); os.makedirs(tdir, exist_ok=True)
    if not found['heightmap']: sys.exit('terrain: no Heightmap component found in the bundles')
    bundle, pid = found['heightmap']
    env, byid = al.env_for_bundle(bundle)
    mref = byid[pid].read_typetree()['m_material']
    mat = al.resolve(byid[pid], mref); mt = mat.read_typetree()
    log('terrain: material', mt['m_Name'])
    for prop, tex in mt['m_SavedProperties']['m_TexEnvs']:
        if prop not in TERRAIN_TEX or not tex['m_Texture']['m_PathID']: continue
        t = al.resolve(mat, tex['m_Texture'])
        if t is None: log('terrain: missing texture', prop); continue
        t.read().image.convert('RGBA').save(os.path.join(tdir, TERRAIN_TEX[prop] + '.png'), optimize=True)
    # The diffuse and normal texture arrays, as vertical strips of 256 x 256 slices.
    for arr, dst in (('terrain_d_array', 'd_array'), ('terrain_n_array', 'n_array')):
        b = found['arrays'].get(arr)
        if not b: sys.exit(f'terrain: texture array {arr} not found')
        e, _ = al.env_for_bundle(b)
        o = next(o for o in e.objects if o.type.name == 'Texture2DArray' and o.peek_name() == arr)
        tt = o.read_typetree()
        fmt, w, h, depth = tt['m_Format'], tt['m_Width'], tt['m_Height'], tt['m_Depth']
        data = bytes(tt['image data']) if tt.get('image data') else b''
        sd = tt.get('m_StreamData', {})
        if not data and sd.get('size'):
            res_name = sd['path'].split('/')[-1]
            for bf in e.files.values():
                for sub, sf in getattr(bf, 'files', {}).items():
                    if sub == res_name:
                        data = sf.bytes[sd['offset']:sd['offset'] + sd['size']] if hasattr(sf, 'bytes') else sf.read_bytes(sd['offset'], sd['size'])
        per = len(data) // depth
        sheet = Image.new('RGBA', (w, h * depth))
        for i in range(depth):
            img = Texture2DConverter.parse_image_data(data[i * per:(i + 1) * per], w, h, UnityPy.enums.TextureFormat(GF.get(fmt, fmt)), (2022, 3, 0, 0), 0, None, True)
            sheet.paste(img.convert('RGBA'), (0, h * i))
        sheet.save(os.path.join(tdir, dst + '.png'), optimize=True)
        log(f'terrain: {arr} ({depth} slices)')
    # Shader: the OpenGL core (platform 15) program blob, then one fragment variant of the deferred pass.
    # Valheim for Windows has no OpenGL programs: there the Vulkan one (platform 18) is taken, named,
    # and written as SPIR-V for the editor to turn into GLSL (heightmap.frag.spv).
    sh = al.resolve(mat, mt['m_Shader']); st = sh.read_typetree()
    blob = bytes(st['compressedBlob'])
    def platform_blob(pi):
        data = b''
        for o_, c, d in zip(flat(st['offsets'][pi]), flat(st['compressedLengths'][pi]), flat(st['decompressedLengths'][pi])):
            data += lz4.block.decompress(blob[o_:o_ + c], uncompressed_size=d)
        return data
    glsl, spv = os.path.join(tdir, 'heightmap.frag.glsl'), os.path.join(tdir, 'heightmap.frag.spv')
    platforms = list(st['platforms'])
    log('terrain: shader programs for platforms', platforms)
    if 15 not in platforms or vulkan:
        if 18 not in platforms:
            sys.exit(f'terrain: the terrain shader has no OpenGL or Vulkan program (platforms {platforms}); '
                     'this copy of the game cannot give the editor its terrain shader')
        if HERE not in sys.path: sys.path.insert(0, HERE)
        import vulkan_shader
        open(spv, 'wb').write(vulkan_shader.terrain_fragment(st, platform_blob(platforms.index(18))))
        if os.path.exists(glsl): os.remove(glsl)
        log('terrain: shader taken from the Vulkan program (the editor turns it into GLSL)')
        return
    if os.path.exists(spv): os.remove(spv)
    data = platform_blob(platforms.index(15))
    d = data.decode('latin1')
    frags = []; i = 0
    while True:
        a = d.find('#ifdef FRAGMENT', i)
        if a < 0: break
        b2 = d.find('#endif\n', d.find('void main', a)); frags.append(d[a:b2]); i = b2
    keep = {}
    for f in frags:
        if '_DiffuseArrayTex' not in f: continue
        pref = 'ds' if 'ds_TEXCOORD0' in f else 'vs'
        full = 'texture(_ClearedMaskTex, %s_TEXCOORD0.xy);' % pref in f
        outs = len(set(re.findall(r'SV_Target(\d) =', f)))
        keep.setdefault((pref, full, outs, 'unity_FogParams' in f), f)
    best = keep.get(('vs', True, 4, False)) or keep.get(('vs', True, 1, False)) or keep.get(('vs', True, 1, True))
    if not best: sys.exit('terrain: no usable fragment variant in the shader')
    src = re.sub(r'[^\x09\x0a\x20-\x7e]', '', best)
    open(os.path.join(tdir, 'heightmap.frag.glsl'), 'w').write(convert_shader(src))
    log('terrain: shader converted')


def convert_shader(src):
    body = src[src.index('uniform \tvec4 _Time;'):]
    body = body[:body.rindex('}') + 1]
    body = re.sub(r'UNITY_LOCATION\(\d+\) uniform ', 'uniform ', body)
    body = re.sub(r'layout\(location = \d\) out vec4 (SV_Target\d);', r'vec4 \1;', body)
    body = body.replace('uniform \tfloat _depth[4];\n', '')
    old = '''    u_xlat66 = _depth[2] + (-_depth[3]);
    u_xlat66 = vs_TEXCOORD0.x * u_xlat66 + _depth[3];
    u_xlat68 = (-_depth[0]) + _depth[1];
    u_xlat68 = vs_TEXCOORD0.x * u_xlat68 + _depth[0];
    u_xlat68 = (-u_xlat66) + u_xlat68;
    u_xlat66 = vs_TEXCOORD0.y * u_xlat68 + u_xlat66;'''
    if body.count(old) != 1: sys.exit('terrain: the shader changed (depth block not found); the converter needs an update')
    body = body.replace(old, '    u_xlat66 = vs_depth;')
    if body.count('texture(_ClearedMaskTex, vs_TEXCOORD0.xy)') != 1: sys.exit('terrain: the shader changed (paint mask); the converter needs an update')
    body = body.replace('texture(_ClearedMaskTex, vs_TEXCOORD0.xy)', 'texture(_ClearedMaskTex, vs_maskUV)')
    if 'vs_TEXCOORD0' in body.replace('in  vec2 vs_TEXCOORD0;', ''): sys.exit('terrain: the shader changed (texcoords); the converter needs an update')
    body = body.replace('in  vec2 vs_TEXCOORD0;', 'in  float vs_depth;\nin  vec2 vs_maskUV;')
    body = body.replace('void main()', 'void valheimGBuffer()', 1)
    hdr = '''// Valheim's terrain shader (Custom/Heightmap, deferred G-buffer pass), compiled by Unity for
// OpenGL core and adapted to WebGL 2: the four G-buffer targets become globals that main() lights,
// the per-zone ocean depth and the paint mask arrive as varyings. Generated; do not edit by hand.
precision highp float;
precision highp int;
precision highp sampler2D;
precision highp sampler3D;
precision highp sampler2DArray;
'''
    return hdr + body + '\n'


# ---- 3. World map textures (material "minimap", shader Custom/mapshader).
MAP_TEX = {'_BackgroundTex': 'background', '_WaterTex': 'water', '_MountainTex': 'mountain', '_ForestTex': 'forest',
           '_CloudTex': 'cloud', '_lavaTex': 'lava', '_SpaceTex': 'space', '_FogLayerTex': 'foglayer'}


def export_map(found, al, out):
    mdir = os.path.join(out, 'maptex'); os.makedirs(mdir, exist_ok=True)
    if not found['minimap']: sys.exit('map: material "minimap" not found')
    b, pid = found['minimap']
    _, byid = al.env_for_bundle(b)
    mat = byid[pid]; mt = mat.read_typetree()
    for prop, tex in mt['m_SavedProperties']['m_TexEnvs']:
        if prop not in MAP_TEX or not tex['m_Texture']['m_PathID']: continue
        t = al.resolve(mat, tex['m_Texture'])
        if t is None: log('map: missing texture', prop); continue
        t.read().image.save(os.path.join(mdir, MAP_TEX[prop] + '.png'))
    log('map: textures written')


# ---- 4. Models of build pieces and world objects.
def world_prefabs(world):
    # zdo_scan.py sits next to this script in a release, one folder up in the source tree.
    for d in (HERE, os.path.dirname(HERE)):
        if os.path.exists(os.path.join(d, 'zdo_scan.py')): sys.path.insert(0, d); break
    import zdo_scan
    seen = collections.Counter()
    for prefab, _, _ in zdo_scan.scan(world): seen[prefab] += 1
    return seen


# Creatures players tame (and their young, born tamed), drawn in the Tamed animals kind. Deer and Neck
# are tamed by server mods.
TAMEABLE = {'Boar', 'Boar_piggy', 'Wolf', 'Wolf_cub', 'Lox', 'Lox_Calf', 'Hen', 'Chicken', 'Asksvin', 'Asksvin_hatchling', 'Deer', 'Neck'}


def export_models(found, args, work, out):
    mdir = os.path.join(out, 'models'); os.makedirs(mdir, exist_ok=True)
    # The build-piece list: next to this script in a release, Core/WorldGen/pieces.json in the source tree.
    plist = next(p for p in (os.path.join(HERE, 'pieces.json'), os.path.join(REPO, 'Core', 'WorldGen', 'pieces.json')) if os.path.exists(p))
    pieces = set(json.load(open(plist)))
    roots = found['roots']
    index = {}
    for n in pieces:
        if n in roots: index[str(stable_hash(n))] = {'name': n, 'bundle': roots[n][0], 'pid': roots[n][1]}
    objects = {}
    if args.objects != 'none':
        wanted = world_prefabs(args.world) if args.objects == 'world' else None
        for n, (b, pid, znv, creature, item) in roots.items():
            # Runestones (locations: the save keeps a proxy, the editor shows the location's model) and the
            # creatures players tame, for the Tamed animals kind; both whatever the world holds.
            extra = n.startswith('Runestone_') or creature and n in TAMEABLE
            if not extra and (n in pieces or not znv or creature or item or n.endswith('_ragdoll')): continue
            h = stable_hash(n)
            if wanted is not None and h not in wanted and not extra: continue
            objects[str(h)] = {'name': n, 'bundle': b, 'pid': pid}
    index.update(objects)
    ipath = os.path.join(work, 'model_index.json'); json.dump(index, open(ipath, 'w'))
    log(f'models: {len(index)} kinds ({len(index) - len(objects)} build pieces, {len(objects)} world objects)')
    env = dict(os.environ, INDEX=ipath)
    # export_pieces.py reuses what is already in the output folder, so an interrupted run continues.
    done = {os.path.splitext(f)[0] for f in os.listdir(os.path.join(mdir, 'pieces'))} if os.path.isdir(os.path.join(mdir, 'pieces')) else set()
    todo = sorted(v['name'] for v in index.values() if v['name'] not in done)
    if todo:
        log(f'models: exporting {len(todo)} (already done: {len(index) - len(todo)})')
        lst = os.path.join(work, 'todo.txt'); open(lst, 'w').write(','.join(todo))
        subprocess.run([sys.executable, os.path.join(HERE, 'export_pieces.py'), mdir, ','.join(todo)], env=env, check=True)
    subprocess.run([sys.executable, os.path.join(HERE, 'fix_normals.py'), mdir], check=True)
    subprocess.run([sys.executable, os.path.join(HERE, 'fix_alpha.py'), mdir], check=True)
    # objects.json: world objects (not pieces) that got a model, hash -> name.
    names = {}
    for h, v in objects.items():
        p = os.path.join(mdir, 'pieces', v['name'] + '.json')
        if os.path.exists(p) and json.load(open(p))['parts']: names[h] = v['name']
    json.dump(names, open(os.path.join(mdir, 'objects.json'), 'w'))
    log(f'models: {len(names)} world objects with a model')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--valheim', required=True, help='the Valheim game folder (with valheim_Data)')
    ap.add_argument('--out', required=True, help="the editor's game-look folder")
    ap.add_argument('--work', help='folder for the scan cache and indexes (default: <out>/../export-cache)')
    ap.add_argument('--objects', choices=('all', 'world', 'none'), default='all')
    ap.add_argument('--world', help='with --objects world: a world folder (with *.chunk files)')
    ap.add_argument('--only', choices=('terrain', 'map', 'models'), action='append', help='run only these steps (repeatable)')
    ap.add_argument('--shader', choices=('auto', 'vulkan'), default='auto',
                    help="vulkan: take the terrain shader from its Vulkan program even when there is an OpenGL one "
                         "(what a Windows copy of the game gives; for testing)")
    args = ap.parse_args()
    if args.objects == 'world' and not args.world: ap.error('--objects world needs --world')
    out = os.path.abspath(args.out)
    work = os.path.abspath(args.work or os.path.join(out, '..', 'export-cache')); os.makedirs(work, exist_ok=True)
    bundles = find_bundles(args.valheim)
    found = scan(bundles, work)
    al = use_assetlib(bundles, work)
    steps = args.only or ['terrain', 'map', 'models']
    if 'terrain' in steps: export_terrain(found, al, out, args.shader == 'vulkan')
    if 'map' in steps: export_map(found, al, out)
    if 'models' in steps: export_models(found, args, work, out)
    log('done.')


if __name__ == '__main__':
    main()
