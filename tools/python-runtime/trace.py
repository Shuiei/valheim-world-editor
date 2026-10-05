"""Make keep-<rid>.txt: the files of the bundled Python runtime that a full game-look export uses.

  python3 tools/python-runtime/trace.py <linux-x64|win-x64> <Valheim folder>

Builds the runtime untrimmed (tools/make-python-runtime.sh with VWE_NO_TRIM=1), runs a full
export of the game with it while sitecustomize.py logs every file opened and every module and
library loaded, then adds the native libraries those load in turn (ELF NEEDED / PE imports).
win-x64 runs under Wine, with no display. Rerun it after changing requirements.txt, the Python
version or the exporter, and commit the new list. Needs readelf (linux) or objdump (win).
"""
import os, re, shutil, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
repo = os.path.dirname(os.path.dirname(here))


def deps(rid, path):
    if rid == 'linux-x64':
        out = subprocess.run(['readelf', '-d', path], capture_output=True, text=True).stdout
        return re.findall(r'\(NEEDED\).*\[(.+?)\]', out)
    out = subprocess.run(['objdump', '-p', path], capture_output=True, text=True).stdout
    return re.findall(r'DLL Name: (\S+)', out)


def main(rid, valheim):
    work = tempfile.mkdtemp(prefix='vwe-trace-')
    try:
        export = os.path.join(work, 'export-game-files')
        os.makedirs(export)
        subprocess.run([os.path.join(repo, 'tools', 'make-python-runtime.sh'), rid, export], check=True,
                       env=dict(os.environ, VWE_NO_TRIM='1'), stdout=subprocess.DEVNULL)
        for f in ('export_all.py', 'assetlib.py', 'export_pieces.py', 'fix_normals.py', 'fix_alpha.py'):
            shutil.copy(os.path.join(repo, 'tools', 'asset-export', f), export)
        shutil.copy(os.path.join(repo, 'tools', 'zdo_scan.py'), export)
        shutil.copy(os.path.join(repo, 'WorldGen', 'pieces.json'), export)
        root = os.path.join(export, 'python')
        log = os.path.join(work, 'trace.log')
        args = [os.path.join(export, 'export_all.py'), '--valheim', valheim, '--out', os.path.join(work, 'out'), '--work', os.path.join(work, 'cache')]
        if rid == 'linux-x64':
            cmd, env = [os.path.join(root, 'bin', 'python3.12'), '-u', *args], dict(os.environ, VWE_TRACE_LOG=log, PYTHONPATH=here)
        else:
            z = lambda p: 'Z:' + p.replace('/', '\\')
            cmd = ['wine', os.path.join(root, 'python.exe'), '-u', *[z(a) if a.startswith('/') else a for a in args]]
            env = {k: v for k, v in os.environ.items() if k not in ('DISPLAY', 'WAYLAND_DISPLAY')}
            env.update(VWE_TRACE_LOG=z(log), PYTHONPATH=z(here), WINEDEBUG='-all', WINEPREFIX=os.environ.get('WINEPREFIX', os.path.join(work, 'wine')))
        subprocess.run(cmd, env=env, check=True)
        if rid == 'win-x64':
            subprocess.run(['wineserver', '-k'], env=env)

        files = {}
        for d, _, fs in os.walk(root):
            for f in fs:
                rel = os.path.relpath(os.path.join(d, f), root).replace(os.sep, '/')
                files[rel.lower() if rid == 'win-x64' else rel] = rel
        prefix = root.lower() if rid == 'win-x64' else root
        used = set()
        for line in open(log, encoding='utf-8', errors='replace'):
            kind, p = line.rstrip('\n').split('\t', 1)
            if rid == 'win-x64':
                p = p[2:].replace('\\', '/').lower() if p[:2].lower() == 'z:' else p.lower()
            if p.startswith(prefix + '/') and '__pycache__' not in p:
                rel = p[len(prefix) + 1:]
                if rel in files:
                    used.add(files[rel])
        used.add('bin/python3.12' if rid == 'linux-x64' else 'python.exe')
        # Native libraries load their own libraries: follow them inside the runtime.
        by_name = {}
        for rel in files.values():
            by_name.setdefault(os.path.basename(rel).lower() if rid == 'win-x64' else os.path.basename(rel), rel)
        todo = [u for u in used if u.endswith(('.so', '.pyd', '.dll', '.exe')) or '.so.' in u or u == 'bin/python3.12']
        while todo:
            for d in deps(rid, os.path.join(root, todo.pop())):
                rel = by_name.get(d.lower() if rid == 'win-x64' else d)
                if rel and rel not in used:
                    used.add(rel)
                    todo.append(rel)
        with open(os.path.join(here, f'keep-{rid}.txt'), 'w', encoding='utf-8', newline='\n') as f:
            f.write(f'# Made by trace.py from a full export: the runtime files the export uses ({rid}).\n')
            f.writelines(u + '\n' for u in sorted(used))
        print(f'keep-{rid}.txt: {len(used)} of {len(files)} files')
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    main(sys.argv[1], os.path.abspath(sys.argv[2]))
