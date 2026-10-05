import UnityPy, glob, os, json, sys
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
files=sorted(glob.glob(B+'*'), key=os.path.getsize)
for f in files:
    try: env=UnityPy.load(f)
    except Exception: continue
    for o in env.objects:
        if o.type.name!='MonoBehaviour': continue
        try: raw=o.get_raw_data()
        except Exception: continue
        if b'ThunderStorm' in raw and b'Misty' in raw:
            m=o.read_typetree()
            if 'm_environments' in m:
                print('FOUND', f, flush=True)
                json.dump(m, open(sys.argv[1],'w'), default=str)
                sys.exit(0)
print('not found')
