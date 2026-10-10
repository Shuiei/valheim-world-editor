# Writes cab_index.json next to this script (each bundle's internal file name -> the bundle), which
# assetlib.py uses to follow references from one bundle into another. Run it once, and again after a
# game update, before the scan_*.py scripts.
# Usage: python3 make_cab_index.py [<bundles folder>]   (default: VWE_BUNDLES, else /opt/Steam's Valheim)
import glob, json, os, sys
import UnityPy

bundles = sys.argv[1] if len(sys.argv) > 1 else os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles')
index = {}
for f in sorted(glob.glob(os.path.join(bundles, '*'))):
    if not os.path.isfile(f): continue
    try: env = UnityPy.load(f)
    except Exception: continue
    for bf in env.files.values():
        for sub in getattr(bf, 'files', {}).keys():
            index[sub.split('.')[0].lower()] = os.path.basename(f)
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cab_index.json')
json.dump(index, open(out, 'w'))
print(f'{len(index)} internal files in {out}')
