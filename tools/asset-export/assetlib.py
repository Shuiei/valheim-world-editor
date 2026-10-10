# Shared helpers for reading Valheim's asset bundles by (internal file, path id).
import UnityPy, json, os
# VWE_BUNDLES: the game's bundle folder; VWE_CAB_INDEX: internal file -> bundle (default cab_index.json
# here, made by make_cab_index.py).
B=os.path.join(os.environ.get('VWE_BUNDLES', '/opt/Steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles'), '')
_idx=os.environ.get('VWE_CAB_INDEX') or os.path.join(os.path.dirname(__file__),'cab_index.json')
IDX=json.load(open(_idx)) if os.path.exists(_idx) else {}
_envs={}
def env_for_bundle(name):
    if name not in _envs:
        e=UnityPy.load(B+name); _envs[name]=(e,{x.path_id:x for x in e.objects})
    return _envs[name]
def resolve(owner, ref):
    # owner: an ObjectReader; ref: {'m_FileID','m_PathID'} -> ObjectReader or None
    if ref['m_PathID']==0: return None
    if ref['m_FileID']==0:
        return env_for_bundle_of(owner)[1].get(ref['m_PathID'])
    cab=owner.assets_file.externals[ref['m_FileID']-1].path.split('/')[-1].split('.')[0].lower()
    b=IDX.get(cab)
    if not b: return None
    return env_for_bundle(b)[1].get(ref['m_PathID'])
_owner_bundle={}
def env_for_bundle_of(obj):
    for name,(e,objs) in _envs.items():
        if objs.get(obj.path_id) is obj: return (e,objs)
    raise KeyError('bundle of object not loaded')
