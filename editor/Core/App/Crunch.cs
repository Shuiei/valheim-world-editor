// Crunch.cs: a C# port of the DXT1 and DXT5 decoding in crunch's inc/crn_decomp.h, as DaemonEngine/crunch has it
// (https://github.com/DaemonEngine/crunch, commit 4449e30), the Unity format of crunch
// (https://github.com/Unity-Technologies/crunch/tree/unity). Altered from the original: see the class summary.
//
// crn_decomp.h uses the ZLIB license:
// http://opensource.org/licenses/Zlib
//
// Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC
//
// This software is provided 'as-is', without any express or implied
// warranty.  In no event will the authors be held liable for any damages
// arising from the use of this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not
// claim that you wrote the original software. If you use this software
// in a product, an acknowledgment in the product documentation would be
// appreciated but is not required.
//
// 2. Altered source versions must be plainly marked as such, and must not be
// misrepresented as being the original software.
//
// 3. This notice may not be removed or altered from any source distribution.

namespace TerrainEditor.App;

/// <summary>
/// Unity's crunched textures (DXT1Crunched, DXT5Crunched) to plain DXT1 and DXT5 blocks: a port of the decoder in
/// crunch's crn_decomp.h (DaemonEngine/crunch, from Unity-Technologies/crunch's "unity" branch; zlib license,
/// Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC). Altered: only DXT1 and DXT5 (crunch's DXN,
/// DXT5A, ETC1 and ETC2 formats are left out), only one face (no cube maps), only Unity's format (Unity 2017.3 and
/// later; the original crunch format of older Unity versions codes its levels differently and is not read), and every
/// length and index the file gives is checked, as the files come from mods.
/// </summary>
public static class Crunch
{
    /// <summary>A crunched texture's size, mip levels and block format, as its header gives them.</summary>
    /// <param name="Width">The top level's width in pixels.</param>
    /// <param name="Height">The top level's height in pixels.</param>
    /// <param name="Levels">How many mip levels it holds.</param>
    /// <param name="Dxt5">DXT5 blocks (16 bytes, with alpha) rather than DXT1 (8 bytes).</param>
    public sealed record Texture(int Width, int Height, int Levels, bool Dxt5);

    /// <summary>The texture a crn file holds, or null when it is not one read here (or broken).</summary>
    public static Texture? Read(byte[] crn)
    {
        try
        {
            var h = new CrunchHeader(crn);
            return new Texture(h.Width, h.Height, h.Levels, h.Format == CrunchFormat.Dxt5);
        }
        catch (InvalidDataException e)
        {
            Log.CaughtBrief(e, "reading a crunched texture's header (not drawn from it)");
            return null;
        }
    }

    /// <summary>
    /// One mip level of a crn file as DXT1 or DXT5 blocks (as <see cref="Read"/> says), rows of blocks one after the
    /// other in the file's order. Null when the file is not one read here, the level is not in it, or the data is
    /// broken.
    /// </summary>
    public static byte[]? Unpack(byte[] crn, int level) => Unpack(crn, _ => level)?.Blocks;

    /// <summary>
    /// The texture a crn file holds and the mip level <paramref name="pick"/> chooses from it, unpacked as
    /// <see cref="Unpack(byte[], int)"/> does: the header read and checked once for both. Null when the file is not one
    /// read here, the level picked is not in it, or the data is broken.
    /// </summary>
    /// <param name="crn">The crn file.</param>
    /// <param name="pick">The level wanted, from the texture's size, levels and format.</param>
    public static (Texture Info, int Level, byte[] Blocks)? Unpack(byte[] crn, Func<Texture, int> pick)
    {
        try
        {
            var h = new CrunchHeader(crn);
            var info = new Texture(h.Width, h.Height, h.Levels, h.Format == CrunchFormat.Dxt5);
            int level = pick(info);
            return level < 0 || level >= h.Levels ? null : (info, level, new CrunchUnpacker(crn, h).Level(level));
        }
        catch (InvalidDataException e)
        {
            Log.CaughtBrief(e, "unpacking a crunched texture (not drawn from it)");
            return null;
        }
    }
}
