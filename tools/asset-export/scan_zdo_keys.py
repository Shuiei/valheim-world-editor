# Names of the data an object (ZDO) can hold (WorldGen/zdo-keys.json, in git): the save only stores
# each name's hash (String.GetStableHashCode), so the editor's object inspector looks the hash up here.
# Every string literal of the game code (the #US heap of assembly_valheim.dll) is a candidate, plus the
# names the game builds at run time ("pu_id" + i for ward permissions, "{index}_item" for armor stands...).
# Usage: python scan_zdo_keys.py <Valheim folder or assembly_valheim.dll> <out.json>
import json, os, re, struct, sys

def stable_hash(s):
    a = b = 5381
    for i in range(0, len(s), 2):
        a = ((a * 33) & 0xFFFFFFFF) ^ ord(s[i])
        if i == len(s) - 1:
            break
        b = ((b * 33) & 0xFFFFFFFF) ^ ord(s[i + 1])
    v = (a + b * 1566083941) & 0xFFFFFFFF
    return v - (1 << 32) if v >= 1 << 31 else v

def user_strings(dll):
    d = open(dll, 'rb').read()
    pe = struct.unpack_from('<I', d, 0x3C)[0]
    nsec = struct.unpack_from('<H', d, pe + 6)[0]
    opt = pe + 24
    magic = struct.unpack_from('<H', d, opt)[0]
    dirs = opt + (96 if magic == 0x10B else 112)
    cli_rva = struct.unpack_from('<I', d, dirs + 14 * 8)[0]
    sec = opt + struct.unpack_from('<H', d, pe + 20)[0]
    sections = [struct.unpack_from('<IIII', d, sec + i * 40 + 8) for i in range(nsec)]  # vsize, va, rawsize, rawptr
    def off(rva):
        for vsize, va, rawsize, rawptr in sections:
            if va <= rva < va + max(vsize, rawsize):
                return rva - va + rawptr
        raise ValueError('rva outside sections')
    cli = off(cli_rva)
    md = off(struct.unpack_from('<I', d, cli + 8)[0])
    assert struct.unpack_from('<I', d, md)[0] == 0x424A5342, 'no CLI metadata'
    vlen = struct.unpack_from('<I', d, md + 12)[0]
    p = md + 16 + vlen + 2
    nstreams = struct.unpack_from('<H', d, p)[0]; p += 2
    for _ in range(nstreams):
        o, size = struct.unpack_from('<II', d, p); p += 8
        e = d.index(b'\0', p); name = d[p:e].decode(); p = (e + 4) & ~3
        if name == '#US':
            heap = d[md + o: md + o + size]
            i = 1
            while i < len(heap):
                b0 = heap[i]
                if b0 & 0x80 == 0: n, i = b0, i + 1
                elif b0 & 0xC0 == 0x80: n, i = ((b0 & 0x3F) << 8) | heap[i + 1], i + 2
                else: n, i = ((b0 & 0x1F) << 24) | (heap[i + 1] << 16) | (heap[i + 2] << 8) | heap[i + 3], i + 4
                if n:
                    yield heap[i:i + n - 1].decode('utf-16-le', 'replace')
                i += n
            return
    raise ValueError('no #US heap')

def main():
    src, out = sys.argv[1], sys.argv[2]
    dll = src if src.endswith('.dll') else os.path.join(src, 'valheim_Data', 'Managed', 'assembly_valheim.dll')
    names = {s for s in user_strings(dll) if re.fullmatch(r'[A-Za-z_][A-Za-z0-9_ ]{0,39}', s)}
    # Built at run time by the game.
    for i in range(64):
        names |= {f'pu_id{i}', f'pu_name{i}', f'{i}_seed', f'data_{i}', f'data__{i}'}
    for i in range(16):
        for n in ('item', 'variant', 'quality', 'itemData', 'data', 'sockets'):
            names.add(f'{i}_{n}')
    # ZDOID values are kept as two longs, name + "_u" and name + "_i".
    for n in list(names):
        names |= {n + '_u', n + '_i'}
    table = {}
    for n in sorted(names, key=lambda s: (len(s), s)):
        table.setdefault(str(stable_hash(n)), n)
    json.dump(table, open(out, 'w'), separators=(',', ':'), sort_keys=True)
    print('names:', len(table))

if __name__ == '__main__':
    main()
