# Build tools (WorldGen/pieces.json "tool"): which pieces each build tool's menu has, from the game's
# PieceTables (_HammerPieceTable, _HoePieceTable, _CultivatorPieceTable, _FeasterPieceTable). The
# editor writes a builder on new pieces of these kinds, as the game does when a player places one.
# Entries that are not objects (repair, remove, the hoe's and cultivator's ground tools) are left out.
# Run after scan_pieces.py and scan_prefabs.py, as it adds to their output.
# Usage: VWE_BUNDLES=<game>/valheim_Data/StreamingAssets/SoftRef/Bundles python scan_build_tools.py <pieces.json> <prefabs.json>
import UnityPy, glob, json, os, sys, gc
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
TOOLS={'_HammerPieceTable':'hammer','_HoePieceTable':'hoe','_CultivatorPieceTable':'cultivator','_FeasterPieceTable':'feaster'}
files=sorted(glob.glob(B+'*'))
tables={}; want={}; cabof={}
# Pass 1: the tables and their piece references (asset file, path id). Bundles are read one at a
# time: all of them at once do not fit in memory.
for f in files:
    try: env=UnityPy.load(f)
    except Exception: continue
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if not hasattr(sf,'objects'): continue
            cabof[name.lower()]=f
            for o in sf.objects.values():
                if o.type.name!='MonoBehaviour': continue
                try: d=o.read_typetree()
                except Exception: continue
                if 'm_pieces' not in d or 'm_canRemovePieces' not in d: continue
                try: owner=sf.objects[d['m_GameObject']['m_PathID']].read_typetree()['m_Name']
                except Exception: continue
                if owner not in TOOLS: continue
                refs=[]
                for p in d['m_pieces']:
                    fid,pid=p['m_FileID'],p['m_PathID']
                    cab=name.lower() if fid==0 else os.path.basename(sf.externals[fid-1].path).lower()
                    refs.append((cab,pid)); want.setdefault(cab,set()).add(pid)
                tables[TOOLS[owner]]=refs
    del env; gc.collect()
# Pass 2: the names of the referenced prefabs.
names={}
for cab,pids in want.items():
    env=UnityPy.load(cabof[cab])
    for af in env.files.values():
        for name, sf in getattr(af,'files',{}).items():
            if name.lower()!=cab or not hasattr(sf,'objects'): continue
            for pid in pids:
                try: names[(cab,pid)]=sf.objects[pid].read_typetree()['m_Name']
                except Exception: pass
    del env; gc.collect()
pieces=json.load(open(sys.argv[1])); prefabs=json.load(open(sys.argv[2]))
for v in pieces.values(): v.pop('tool',None)
for tool in ['feaster','cultivator','hoe','hammer']:   # a piece in several menus: the hammer wins
    for r in tables.get(tool,[]):
        n=names.get(r)
        if n in pieces and n in prefabs: pieces[n]['tool']=tool
json.dump(pieces,open(sys.argv[1],'w'),separators=(', ',': '))
print({t:sum(1 for v in pieces.values() if v.get('tool')==t) for t in TOOLS.values()})
