"""Remove every file of the bundled Python runtime that the game-look export does not use.

  python3 tools/python-runtime/trim.py <linux-x64|win-x64> <runtime folder>

What stays:
  - the files in keep-<rid>.txt: what a traced full export opened or loaded (see trace.py), and
    the native libraries those need;
  - the whole `encodings` package: Python picks one at start from the system's language;
  - in site-packages, the Python files and metadata of every package the export uses (a game update
    may need another of their modules), but only the native files that were loaded, with their
    builds for other processors (`_none`, `_sse41`, `_avx2`...: one is picked per computer);
  - licence files.
"""
import os, re, sys

CPU = re.compile(r'_(none|sse2|sse41|sse4_1|avx|avx2|avx512)(?=[._])')
NATIVE = ('.so', '.pyd', '.dll', '.dylib')


def native(p):
    return p.endswith(NATIVE) or '.so.' in os.path.basename(p)


def main(rid, root):
    here = os.path.dirname(os.path.abspath(__file__))
    keep = {l.strip() for l in open(os.path.join(here, f'keep-{rid}.txt'), encoding='utf-8') if l.strip() and not l.startswith('#')}
    site = 'lib/python3.12/site-packages/' if rid == 'linux-x64' else 'Lib/site-packages/'
    stdlib = 'lib/python3.12/' if rid == 'linux-x64' else 'Lib/'
    files = []
    for d, _, fs in os.walk(root):
        for f in fs:
            files.append(os.path.relpath(os.path.join(d, f), root).replace(os.sep, '/'))
    # Packages the export uses: the first folder (or module) under site-packages of a kept file.
    used = {p[len(site):].split('/')[0] for p in keep if p.startswith(site)}
    # A package's metadata goes with it: the dist-info folders whose RECORD lists a used package.
    meta = set()
    for p in files:
        if p.startswith(site) and p.endswith('.dist-info/RECORD'):
            listed = {l.split(',')[0].split('/')[0] for l in open(os.path.join(root, p), encoding='utf-8')}
            if listed & used:
                meta.add(p[len(site):].split('/')[0])
    kept_native = {p for p in keep if native(p)}

    def stays(p):
        if p in keep:
            return True
        name = os.path.basename(p)
        if p.startswith(site):
            top = p[len(site):].split('/')[0]
            if top.endswith('.dist-info'):
                return top in meta
            if top not in used or '__pycache__' in p:
                return False
            if not native(p):
                return True
            if CPU.search(name):
                return any(os.path.dirname(k) == os.path.dirname(p) and CPU.sub('_*', os.path.basename(k)).split('.')[0] == CPU.sub('_*', name).split('.')[0] for k in kept_native)
            return False
        if p.startswith(stdlib + 'encodings/') and '__pycache__' not in p:
            return True
        return bool(re.match(r'(LICENSE|COPYING|NOTICE)', name, re.I))

    removed = 0
    for p in files:
        if not stays(p):
            os.remove(os.path.join(root, p))
            removed += 1
    for d, ds, fs in os.walk(root, topdown=False):
        if not os.listdir(d):
            os.rmdir(d)
    missing = [p for p in keep if not os.path.exists(os.path.join(root, p))]
    if missing:
        sys.exit('keep list names files the runtime does not have (rebuild the list with trace.py):\n  ' + '\n  '.join(sorted(missing)))
    print(f'trimmed: removed {removed} of {len(files)} files')


if __name__ == '__main__':
    main(sys.argv[1], os.path.abspath(sys.argv[2]))
