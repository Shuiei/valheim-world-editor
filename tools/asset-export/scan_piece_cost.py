# What every build piece costs (Core/WorldGen/piece-cost.json, in git), for the blueprint library's
# material cost: its English name, the crafting station it needs nearby (Piece.m_craftingStation) and
# its resources (Piece.m_resources: item and amount), items by prefab name with their English names.
# English comes from the game's localization tables (resources.assets).
# Usage: VWE_GAME=<game>/valheim_Data python scan_piece_cost.py <out.json>  (VWE_BUNDLES as the other scans)
import UnityPy, glob, json, sys, csv, io
import os as _os
G = _os.environ.get('VWE_GAME', '/opt/Steam/steamapps/common/Valheim/valheim_Data')
B = _os.path.join(_os.environ.get('VWE_BUNDLES', G + '/StreamingAssets/SoftRef/Bundles'), '')

english = {}
for o in UnityPy.load(G + '/resources.assets').objects:
    if o.type.name != 'TextAsset':
        continue
    d = o.read()
    if not d.m_Name.startswith('localization'):
        continue
    s = d.m_Script if isinstance(d.m_Script, str) else d.m_Script.decode('utf-8', 'replace')
    for row in csv.reader(io.StringIO(s.lstrip('﻿'))):
        if len(row) > 1 and row[0] and row[1]:
            english.setdefault(row[0].strip(), row[1].strip())


def en(token):
    t = (token or '').strip()
    return english.get(t[1:], t) if t.startswith('$') else t


pieces, items = {}, {}
for f in sorted(glob.glob(B + '*')):
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

    def owner(ref):
        # The GameObject a component reference (same file only) sits on: (its name, the component).
        if ref.get('m_FileID', 0) != 0:
            return None, None
        t, c = tt(ref['m_PathID'])
        if c is None:
            return None, None
        _, g = tt(c['m_GameObject']['m_PathID'])
        return (g['m_Name'] if g else None), c

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
        res = []
        for r in p['m_resources']:
            name, drop = owner(r['m_resItem'])
            if not name or r.get('m_amount', 0) <= 0:
                continue
            items.setdefault(name, en(drop.get('m_itemData', {}).get('m_shared', {}).get('m_name', name)))
            res.append([name, r['m_amount'], r.get('m_recover', 1)])
        _, station = owner(p['m_craftingStation'])
        entry = {'n': en(p.get('m_name', go['m_Name'])), 'r': res}
        if station:
            entry['st'] = en(station.get('m_name', ''))
        pieces[go['m_Name']] = entry
json.dump({'pieces': pieces, 'items': items}, open(sys.argv[1], 'w'), separators=(',', ':'), ensure_ascii=False, sort_keys=True)
print('pieces:', len(pieces), 'items:', len(items), 'english strings:', len(english))
for n in ['woodwall', 'wood_floor', 'stone_wall_2x1', 'piece_workbench', 'iron_wall_2x2', 'blackmarble_floor']:
    print(n, json.dumps(pieces.get(n), ensure_ascii=False))
