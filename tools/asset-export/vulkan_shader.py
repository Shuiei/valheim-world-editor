"""The terrain shader's Vulkan program (SPIR-V), for game copies without its OpenGL program.

Valheim for Windows carries its shaders for Direct3D 11 and Vulkan only (the Linux and Mac copies
also for OpenGL). Unity stores each Vulkan program SMOL-V compressed (smolv.py) and without the names
of its uniforms and textures; those are in a parameter blob next to it. This picks the program the
OpenGL path uses (the deferred pass, no keywords, no tessellation), puts the names back into the
SPIR-V (OpName, OpMemberName) and returns it. The editor turns it into GLSL (Core/App/TerrainShader.cs).
"""
import re, struct

import smolv

SPIRV = 25  # ShaderGpuProgramType.kShaderGpuProgramSPIRV
VULKAN = 18  # ShaderCompilerPlatform.kShaderCompPlatformVulkan
FRAGMENT, TESS_CONTROL, TESS_EVAL = 4, 1, 2


def entries(blob):
    """The blob's entries (programs and parameter blobs), as bytes."""
    n = struct.unpack_from('<I', blob, 0)[0]
    out = []
    for i in range(n):
        o, l, _ = struct.unpack_from('<3I', blob, 4 + 12 * i)
        out.append(blob[o:o + l])
    return out


def instructions(words):
    i = 5
    while i < len(words):
        n = words[i] >> 16
        if n == 0: raise ValueError('bad SPIR-V')
        yield i, words[i] & 0xFFFF, words[i + 1:i + n]
        i += n


def string_words(s):
    b = s.encode() + b'\0'
    b += b'\0' * (-len(b) % 4)
    return list(struct.unpack('<%dI' % (len(b) // 4), b))


def read_string(words):
    b = struct.pack('<%dI' % len(words), *words)
    return b[:b.index(0)].decode('latin1')


# ---- Parameter blob: the names of the constant buffers' members and of the textures.
_NAME = re.compile(rb'[A-Za-z_][A-Za-z0-9_]*')


def parameters(entry):
    """{'buffers': {name: {offset: member name}}, 'textures': {binding: name}}.

    Records are a length-prefixed name (padded to 4 bytes) followed by numbers: a constant buffer
    (size, member count), a member (type, rows, columns, is matrix, array size, offset), a
    texture (0, binding with flags in the top byte, sampler, dimension)."""
    recs = []
    p = 24
    while p + 4 <= len(entry):
        n = struct.unpack_from('<I', entry, p)[0]
        if 0 < n < 128 and p + 4 + n <= len(entry) and _NAME.fullmatch(entry[p + 4:p + 4 + n]):
            name = entry[p + 4:p + 4 + n].decode()
            q = p + 4 + n + (-n % 4)
            recs.append([name, q, []])
            p = q
            continue
        if recs: recs[-1][2].append(n)
        p += 4
    buffers, textures = {}, {}
    i = 0
    while i < len(recs):
        name, _, nums = recs[i]
        if 'Globals' in name and len(nums) >= 2 and 0 < nums[1] < len(recs) - i and all(len(r[2]) >= 6 for r in recs[i + 1:i + 1 + nums[1]]):
            members = {}
            for mname, _, m in recs[i + 1:i + 1 + nums[1]]:
                members[m[5]] = mname
            buffers[name] = members
            i += 1 + nums[1]
            continue
        if 'Globals' not in name and len(nums) >= 4 and nums[0] == 0:
            textures[nums[1] & 0xFFFF] = name
        i += 1
    return {'buffers': buffers, 'textures': textures}


# ---- The variant: the deferred pass with no keywords and no tessellation.
def pick(parsed, blob_entries):
    """(fragment SPIR-V words, parameter entry) of the variant the OpenGL path draws, or None."""
    names = parsed.get('m_KeywordNames') or []
    for sub in parsed['m_SubShaders']:
        for ps in sub['m_Passes']:
            if ps['m_State']['m_Name'] != 'DEFERRED': continue
            prog = ps['progVertex']
            for li, lst in enumerate(prog['m_PlayerSubPrograms']):
                for j, sp in enumerate(lst):
                    if sp['m_GpuProgramType'] != SPIRV or sp['m_KeywordIndices']: continue
                    stages = {}
                    for _, w in smolv.programs(blob_entries[sp['m_BlobIndex']]):
                        for _, op, a in instructions(w):
                            if op == 15: stages[a[0]] = w; break
                    if FRAGMENT not in stages or TESS_CONTROL in stages or TESS_EVAL in stages: continue
                    params = blob_entries[prog['m_ParameterBlobIndices'][li][j]]
                    return stages[FRAGMENT], params, [names[k] for k in sp['m_KeywordIndices']]
    return None


def named(words, params):
    """The SPIR-V with names for the uniform buffer's members, the textures, the inputs (the
    unnamed one is vs_COLOR0, as in the OpenGL program) and the outputs (SV_Target0..3)."""
    names, member_names, deco, member_off = {}, {}, {}, {}
    pointer, variables, block = {}, {}, set()
    for _, op, a in instructions(words):
        if op == 5: names[a[0]] = read_string(a[1:])
        elif op == 6: member_names[(a[0], a[1])] = read_string(a[2:])
        elif op == 71:
            deco.setdefault(a[0], {})[a[1]] = a[2:]
            if a[1] == 2: block.add(a[0])
        elif op == 72 and a[2] == 35: member_off.setdefault(a[0], {})[a[1]] = a[3]
        elif op == 32: pointer[a[0]] = (a[1], a[2])  # OpTypePointer: id, storage, type
        elif op == 59: variables[a[1]] = (a[0], a[2])  # OpVariable: result, (pointer type, storage)
    add = []
    # The uniform buffer: its members by offset, from the buffer whose offsets all match.
    for var, (ptype, storage) in variables.items():
        st = pointer.get(ptype, (None, None))[1]
        if storage == 2 and st in block:
            offs = member_off.get(st, {})
            for buf in params['buffers'].values():
                if all(o in buf for o in offs.values()):
                    for idx, o in offs.items(): add.append((6, [st, idx] + string_words(buf[o])))
                    break
            else:
                raise ValueError('the uniform buffer matches none of the parameter blob\'s')
            add.append((5, [var] + string_words('_Globals')))
    for var, (ptype, storage) in variables.items():
        d = deco.get(var, {})
        if storage == 0 and 33 in d:  # UniformConstant with a Binding: a texture
            name = params['textures'].get(d[33][0])
            if not name: raise ValueError(f'no texture for binding {d[33][0]}')
            add.append((5, [var] + string_words(name)))
        elif storage == 1 and 30 in d and var not in names:  # Input
            add.append((5, [var] + string_words('vs_COLOR0')))
        elif storage == 3 and 30 in d:  # Output
            add.append((5, [var] + string_words(f'SV_Target{d[30][0]}')))
    # Names go after the existing debug names, before the first decoration.
    out = list(words[:5]); done = False
    for i, op, a in instructions(words):
        if not done and op in (71, 72, 73, 74, 75, 332, 5632, 5633):
            for o, ws in add: out.append(((len(ws) + 1) << 16) | o); out.extend(ws)
            done = True
        out.append(words[i]); out.extend(a)
    return out


def terrain_fragment(shader_tree, blob):
    """The named SPIR-V (bytes) of the terrain's deferred fragment program, from the Vulkan blob."""
    e = entries(blob)
    got = pick(shader_tree['m_ParsedForm'], e)
    if not got: raise ValueError('no Vulkan program of the deferred pass without keywords')
    words, params, _ = got
    words = named(words, parameters(params))
    return struct.pack('<%dI' % len(words), *words)
