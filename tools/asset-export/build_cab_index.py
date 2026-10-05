import UnityPy, glob, os, json, sys
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
idx={}
files=sorted(glob.glob(B+'*'))
for i,f in enumerate(files):
    try:
        e=UnityPy.load(f)
        for name, bf in e.files.items():
            for sub in getattr(bf, 'files', {}).keys():
                idx[sub.split('.')[0].lower()]=os.path.basename(f)
    except Exception as ex:
        pass
    if i%100==0: print(i, len(idx), file=sys.stderr)
json.dump(idx, open('/tmp/claude-1000/-home-thibs-Desktop-Valheim-BepInEx/992e9bfc-61d8-4ba1-b402-d95d5d70ed95/scratchpad/cab_index.json','w'))
print('indexed', len(idx), 'internal files in', len(files), 'bundles')
