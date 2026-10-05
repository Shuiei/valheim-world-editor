# Loaded by Python at start when this folder is on PYTHONPATH (trace.py only, never shipped): logs
# every file the process opens, every library loaded through ctypes and, at exit, every module.
import atexit, os, sys

_log, _seen = os.environ['VWE_TRACE_LOG'], set()


def _write(kind, path):
    if (kind, path) not in _seen:
        _seen.add((kind, path))
        with open(_log, 'a', encoding='utf-8') as f:
            f.write(f'{kind}\t{path}\n')


def _hook(event, args):
    if event == 'open' and isinstance(args[0], (str, bytes)):
        _write('open', os.fsdecode(args[0]))
    elif event == 'ctypes.dlopen' and args[0]:
        _write('dlopen', str(args[0]))


@atexit.register
def _modules():
    for m in list(sys.modules.values()):
        if getattr(m, '__file__', None):
            _write('module', m.__file__)


sys.addaudithook(_hook)
