using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public static partial class BuildingPicture
{
    /// <summary>The piece's colliders as boxes in its material's colour (a stand-in box when the game has none).</summary>
    private static void AddBoxes(List<Tri> tris, Piece p)
    {
        var (material, boxes) = Stability.Shape(p.Name);
        var colour = material >= 0 && material < MaterialColours.Length ? MaterialColours[material] : new Vector3(130, 130, 130);
        if (boxes.Count == 0)
        {
            boxes.Add((new Vector3(0, 1, 0), new Vector3(0.5f, 1, 0.5f), Quaternion.Identity));
        }
        var rot = BlueprintFormats.FromEuler(p.Euler);
        float s = p.Scale > 0 ? p.Scale : 1;
        foreach (var (c, h, q) in boxes)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var local = c + Vector3.Transform(new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z), q);
                var w = p.Position + Vector3.Transform(local * s, rot);
                corners[i] = new Vector3(w.X, w.Y, -w.Z);
            }
            // The six faces, two triangles each.
            int[][] faces = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];
            foreach (int[] f in faces)
            {
                tris.Add(new Tri { A = corners[f[0]], B = corners[f[1]], C = corners[f[2]], Colour = colour });
                tris.Add(new Tri { A = corners[f[0]], B = corners[f[2]], C = corners[f[3]], Colour = colour });
            }
        }
    }
}
