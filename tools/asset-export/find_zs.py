import UnityPy, glob, os, json, sys
B='/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles/'
for f in sorted(glob.glob(B+'*'), key=os.path.getsize):
    try: env=UnityPy.load(f)
    except Exception: continue
    for o in env.objects:
        if o.type.name!='MonoBehaviour': continue
        try: raw=o.get_raw_data()
        except Exception: continue
        if len(raw)<20000: continue
        try: m=o.read_typetree()
        except Exception: continue
        if 'm_vegetation' in m and isinstance(m['m_vegetation'],list) and len(m['m_vegetation'])>50:
            print('FOUND',f,len(m['m_vegetation']),flush=True)
            json.dump(m['m_vegetation'],open(sys.argv[1],'w'),default=str)
            json.dump({'file':os.path.basename(f),'pid':o.path_id},open(sys.argv[1]+'.src','w'))
            sys.exit(0)
print('not found')
