# What the editor's Dungeon mode needs of the game's dungeons (Core/WorldGen/dungeon-rooms.json, in
# git). A dungeon (DG_Cave, DG_SunkenCrypt...) is a DungeonGenerator object whose saved "roomData" lists
# rooms by the stable hash of their prefab name, with position and rotation; the game rebuilds them from
# that list (DungeonGenerator.Load/Spawn). For each dungeon kind: its algorithm, room themes and the box
# its rooms must stay in (m_zoneSize, around the zone centre at the dungeon's height). For each
# room prefab (a root with a Room component):
#   theme, size, endCap, entrance, divider, endCapPrio, weight, perimeter, enabled;
#   openings (RoomConnection children): [type, entrance, allowDoor, onlyIfOtherAllowsDoor, x, y, z, qx,
#     qy, qz, qw] in the room's frame (the game joins two rooms where openings meet: the new room turned
#     so its opening faces the other, DungeonGenerator.PlaceRoom/CalculateRoomPosRot);
#   contents: the networked objects (ZNetView children) the game makes when it first generates the
#     room (DungeonGenerator.PlaceRoom, SpawnMode.Full): [prefab, x, y, z, qx, qy, qz, qw, node], and
#     what decides whether each is there, in the order the game rolls them: spawns (RandomSpawn:
#     [node, chance %, required theme, off node, min elevation, max elevation]) and picks (RandomObject:
#     [node, required theme, [[choice node, weight]...], min elevation, max elevation]), with nodes: the
#     parent of every node that matters (-1 the room). A dungeon kind's baseSeed: its seed is added to
#     each room's roll (m_addBaseSeedToRandomSpawn).
# Only what is active and enabled in the prefab counts (Utils.GetEnabledComponentsInChildren).
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_dungeon_rooms.py <out.json>
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


def prefab_name(name):
    # Utils.GetPrefabName: the name up to the first '(' or space.
    for i, c in enumerate(name):
        if c in '( ':
            return name[:i]
    return name


def kind(tt):
    if 'm_theme' in tt and 'm_endCap' in tt and 'm_size' in tt: return 'room'
    if 'm_entrance' in tt and 'm_allowDoor' in tt: return 'connection'
    if 'm_persistent' in tt and 'm_distant' in tt: return 'netview'
    if 'm_chanceToSpawn' in tt and 'm_OffObject' in tt: return 'spawn'
    if 'm_objects' in tt and 'm_dungeonRequireTheme' in tt and 'm_chanceToSpawn' not in tt: return 'pick'
    if 'm_algorithm' in tt and 'm_themes' in tt and 'm_maxRooms' in tt: return 'generator'
    return None


rooms, dungeons = {}, {}
files = sorted(glob.glob(B + '*'))
for fi, f in enumerate(files):
    try:
        env = UnityPy.load(f)
    except Exception:
        continue
    byid = {o.path_id: o for o in env.objects}
    tts = {}

    def tt(pid):
        if pid not in tts:
            o = byid.get(pid)
            try: tts[pid] = o.read_typetree() if o is not None else None
            except Exception: tts[pid] = None
        return tts[pid]

    def parts(go):
        tr = None; behaviours = []
        for c in go['m_Component']:
            pid = c['component']['m_PathID']
            o = byid.get(pid)
            if o is None: continue
            if o.type.name == 'Transform': tr = tt(pid)
            elif o.type.name == 'MonoBehaviour':
                t = tt(pid)
                if t is not None: behaviours.append((pid, t))
        return tr, behaviours

    for o in env.objects:
        if o.type.name != 'GameObject': continue
        go = tt(o.path_id)
        if go is None: continue
        tr, behaviours = parts(go)
        if tr is None or tr['m_Father']['m_PathID'] != 0: continue
        kinds = {kind(b): b for _, b in behaviours}
        name = go['m_Name']
        if 'generator' in kinds:
            g = kinds['generator']
            dungeons[name] = {'algorithm': g['m_algorithm'], 'themes': g['m_themes'], 'maxRooms': g['m_maxRooms'],
                              'minRooms': g['m_minRooms'], 'tileWidth': r(g.get('m_tileWidth', 8)),
                              'zoneSize': [r(g['m_zoneSize']['x']), r(g['m_zoneSize']['y']), r(g['m_zoneSize']['z'])],
                              'customInterior': int(g.get('m_useCustomInteriorTransform', 0)),
                              'baseSeed': int(g.get('m_addBaseSeedToRandomSpawn', 0))}
        if 'room' not in kinds or name in rooms: continue
        room = kinds['room']
        info = {'theme': room['m_theme'], 'size': [room['m_size']['x'], room['m_size']['y'], room['m_size']['z']],
                'endCap': int(room['m_endCap']), 'entrance': int(room['m_entrance']), 'divider': int(room.get('m_divider', 0)),
                'endCapPrio': room.get('m_endCapPrio', 0), 'weight': r(room.get('m_weight', 1)),
                'perimeter': int(room.get('m_perimeter', 0)), 'enabled': int(room.get('m_enabled', 1)),
                'openings': [], 'contents': [], 'spawns': [], 'picks': [], 'nodes': []}
        node_of = {}  # GameObject path id -> node index (only nodes that matter get one)
        parent_of = {}
        go_pid = o.path_id

        def node(pid):
            if pid == go_pid: return -1
            if pid not in node_of:
                p = node(parent_of[pid])
                node_of[pid] = len(info['nodes'])
                info['nodes'].append(p)
            return node_of[pid]

        # Depth first, in child order: the order GetComponentsInChildren returns, so the order the
        # game rolls the random parts in. Inactive objects and their children are skipped.
        def walk(gid, pos, rot, root):
            g = tt(gid)
            t, bs = parts(g)
            if not root:
                if not g.get('m_IsActive', True): return
                lp, lr = t['m_LocalPosition'], t['m_LocalRotation']
                d = qrot(rot, (lp['x'], lp['y'], lp['z']))
                pos = (pos[0] + d[0], pos[1] + d[1], pos[2] + d[2])
                rot = qmul(rot, (lr['x'], lr['y'], lr['z'], lr['w']))
            for pid, b in bs:
                if not b.get('m_Enabled', 1): continue
                k = kind(b)
                if root and k != 'connection': continue
                if k == 'connection':
                    info['openings'].append([b['m_type'], int(b['m_entrance']), int(b['m_allowDoor']), int(b.get('m_doorOnlyIfOtherAlsoAllowsDoor', 0)),
                                             r(pos[0]), r(pos[1]), r(pos[2]), r(rot[0]), r(rot[1]), r(rot[2]), r(rot[3])])
                elif k == 'netview':
                    info['contents'].append([prefab_name(g['m_Name']), r(pos[0]), r(pos[1]), r(pos[2]), r(rot[0]), r(rot[1]), r(rot[2]), r(rot[3]), node(gid)])
                elif k == 'spawn':
                    off = b['m_OffObject']['m_PathID']
                    info['spawns'].append([node(gid), r(b['m_chanceToSpawn']), b.get('m_dungeonRequireTheme', 0), node(off) if off and off in parent_of else -1,
                                           b.get('m_minElevation', -10000), b.get('m_maxElevation', 10000)])
                elif k == 'pick':
                    choices = []
                    for e in b['m_objects']:
                        cid = e['m_object']['m_PathID']
                        if cid and cid in parent_of:
                            choices.append([node(cid), r(e['m_weight'])])
                    info['picks'].append([node(gid), b.get('m_dungeonRequireTheme', 0), choices, b.get('m_minElevation', -10000), b.get('m_maxElevation', 10000)])
            for c in t['m_Children']:
                ct = tt(c['m_PathID'])
                if ct is None: continue
                child = ct['m_GameObject']['m_PathID']
                parent_of[child] = gid
                walk(child, pos, rot, False)

        # Parents first, so a node can be numbered when a random part names it before the walk gets there.
        def index(gid):
            t, _ = parts(tt(gid))
            for c in t['m_Children']:
                ct = tt(c['m_PathID'])
                if ct is None: continue
                parent_of[ct['m_GameObject']['m_PathID']] = gid
                index(ct['m_GameObject']['m_PathID'])
        index(go_pid)
        walk(go_pid, (0, 0, 0), (0, 0, 0, 1), True)
        rooms[name] = info
    if fi % 100 == 0:
        print(f'{fi}/{len(files)} bundles, {len(rooms)} rooms', file=sys.stderr, flush=True)

json.dump({'dungeons': dict(sorted(dungeons.items())), 'rooms': dict(sorted(rooms.items()))}, open(sys.argv[1], 'w'), separators=(',', ':'))
print(f'{len(dungeons)} dungeon kinds, {len(rooms)} rooms', file=sys.stderr)
