"""SMOL-V to SPIR-V, for Unity's Vulkan shaders (Unity stores each Vulkan program SMOL-V compressed).

A Python port of the decoder in SMOL-V by Aras Pranckevicius (https://github.com/aras-p/smol-v,
MIT license, Copyright (c) 2016-2024 Aras Pranckevicius), encoding versions 0 and 1.
"""
import struct

# Per SPIR-V op (0..366): has result id, has type id, ids after them stored as deltas from the
# result, rest of the words as varints (SMOL-V's kSpirvOpData table).
_TABLE = '''
000011000000000100000000000000000001110000001000110111210001000100010001110010011001100110011001
100110011001100110011001100110011001100110011001100110011001000111001100110011001190110111001100
110011001100119011001100110111000000119011001101110011110021000000001101110011001100110011000001
000110000000000011001111112111211190111111211110110011001100112111211131113111211121113111311121
113111311121003111101110111011201110112011101110110011101110111011101110111011101110111011101110
111011101110111111101100111011101120112011201120112011201120112011201120112011201120112011201120
112011201120112011201120112011201120110011101110111011101110111011101120112011201120112011201120
111011301120112011201120112011201120112011201120112011201120112011201120112011201120112011201120
110011001120112011201120112011201110114011301130111011101100110011001100110011001100110011001100
110011000000000000000000110011000030002011001100000011001100110011001100110011001100110011001100
110011001100110011001100002100111000001000310000000000000000000000000000110011000000110011001100
110011001100110011001100110011001100110011001100110011001100110000000000110011001100110011000000
000011001100110011001100110011001100000000001100110000000000110011001121112111311131112111211131
113111211131113111100000110000001100110011001100110011001100110011010021110000010001111111111111
111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111
1111111111111111111111111111'''.replace('\n', '')
OPS = [tuple(int(c) for c in _TABLE[i:i + 4]) for i in range(0, len(_TABLE), 4)]
KNOWN = {0: 331, 1: 367}  # ops known by encoding version 0 (up to ModuleProcessed) and 1

SMOL_MAGIC = b'LOMS'  # 0x534D4F4C, little endian
SPIRV_MAGIC = 0x07230203

# The most common ops are swapped with rare small ones, to fit a one-byte varint.
_SWAP = {}
for a, b in ((71, 0), (61, 1), (62, 2), (65, 3), (79, 4), (72, 7), (248, 8), (59, 9), (133, 10), (129, 11), (32, 14), (127, 15)):
    _SWAP[a] = b; _SWAP[b] = a
SHUFFLE, SHUFFLE_COMPACT, DECORATE, MEMBER_DECORATE, LOAD, ACCESS_CHAIN = 79, 13, 71, 72, 61, 65


def decoded_size(data, at=0):
    """The SPIR-V size in bytes of the SMOL-V program at data[at:], or 0 when it is not one."""
    if data[at:at + 4] != SMOL_MAGIC or len(data) < at + 24: return 0
    return struct.unpack_from('<I', data, at + 20)[0]


def decode(data, at=0):
    """The SPIR-V words of the SMOL-V program starting at data[at:]. Reads only as far as the
    program goes, so data may hold more after it."""
    size = decoded_size(data, at)
    if not size: raise ValueError('not SMOL-V data')
    version, generator, bound, schema = struct.unpack_from('<4I', data, at + 4)
    smol_version = version >> 24
    if smol_version not in KNOWN: raise ValueError(f'SMOL-V encoding version {smol_version} is not known')
    known = KNOWN[smol_version]
    out = [SPIRV_MAGIC, version & 0xFFFFFF, generator, bound, schema]
    want = size // 4
    p = at + 24
    n = len(data)

    def varint():
        nonlocal p
        v = shift = 0
        while p < n:
            b = data[p]; p += 1
            v |= (b & 127) << shift; shift += 7
            if not b & 128: break
        return v

    def zig(u):
        return (u >> 1) ^ -1 if u & 1 else u >> 1

    def word():
        nonlocal p
        if p + 4 > n: raise ValueError('SMOL-V data ends early')
        v = struct.unpack_from('<I', data, p)[0]; p += 4
        return v

    prev_result = prev_decorate = 0
    while len(out) < want:
        if p >= n: raise ValueError('SMOL-V data ends early')
        v = varint()
        length = ((v >> 20) << 4) | ((v >> 4) & 0xF)
        op = _SWAP.get(((v >> 4) & 0xFFF0) | (v & 0xF), ((v >> 4) & 0xFFF0) | (v & 0xF))
        length += 1
        if op in (SHUFFLE, SHUFFLE_COMPACT): length += 4
        elif op == DECORATE: length += 2
        elif op in (LOAD, ACCESS_CHAIN): length += 3
        swizzle = op == SHUFFLE_COMPACT
        if swizzle: op = SHUFFLE
        out.append((length << 16) | op)
        info = OPS[op] if op < known else (0, 0, 0, 0)
        i = 1
        if info[1]:
            out.append(varint()); i += 1
        if info[0]:
            prev_result = (prev_result + zig(varint())) & 0xFFFFFFFF
            out.append(prev_result); i += 1
        if op in (DECORATE, MEMBER_DECORATE):
            prev_decorate = (prev_decorate + zig(varint())) & 0xFFFFFFFF
            out.append(prev_decorate); i += 1
        if op == MEMBER_DECORATE:
            count = data[p]; p += 1
            prev_index = prev_offset = 0
            for m in range(count):
                index = varint() + prev_index; prev_index = index
                dec = varint()
                extra = 0 if dec == 0 or 2 <= dec <= 5 else 1 if 29 <= dec <= 37 else -1
                mlen = varint() + 4 if extra < 0 else 4 + extra
                if m:
                    out.append((mlen << 16) | op); out.append(prev_decorate)
                out.append(index); out.append(dec)
                if dec == 35:  # Offset
                    if mlen != 5: raise ValueError('bad SMOL-V member offset')
                    prev_offset = varint() + prev_offset
                    out.append(prev_offset)
                else:
                    for _ in range(4, mlen): out.append(varint())
            continue
        for _ in range(info[2]):
            if i >= length: break
            out.append((prev_result - zig(varint())) & 0xFFFFFFFF); i += 1
        if swizzle and length <= 9:
            s = data[p]; p += 1
            for k, sh in ((5, 6), (6, 4), (7, 2), (8, 0)):
                if length > k: out.append((s >> sh) & 3)
        elif info[3]:
            while i < length: out.append(varint()); i += 1
        else:
            while i < length: out.append(word()); i += 1
    if len(out) != want: raise ValueError('SMOL-V data decodes to the wrong size')
    return out


def programs(data):
    """(offset, SPIR-V words) of every SMOL-V program in a blob."""
    at = data.find(SMOL_MAGIC)
    while at >= 0:
        if decoded_size(data, at):
            yield at, decode(data, at)
        at = data.find(SMOL_MAGIC, at + 4)
