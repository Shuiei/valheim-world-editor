
namespace TerrainEditor.Desktop;

/// <summary>
/// The look's meshes: the sky's sphere and the water's plane, sent to the graphics card.
/// </summary>
public sealed partial class GameLookGl
{
    /// <summary>
    /// Makes the sky's mesh: a sphere of radius 2000 m, which the sky's vertex shader keeps around the camera.
    /// </summary>
    private unsafe void BuildSky()
    {
        // A sphere around the camera (drawn at the far plane), 48 × 24 segments like look.js.
        const int SegW = 48, SegH = 24;
        const float R = 2000;
        var v = new List<float>();
        var idx = new List<uint>();
        for (int y = 0; y <= SegH; y++)
        {
            for (int x = 0; x <= SegW; x++)
            {
                float u = x / (float)SegW, vv = y / (float)SegH;
                v.Add(-R * MathF.Cos(u * MathF.Tau) * MathF.Sin(vv * MathF.PI));
                v.Add(R * MathF.Cos(vv * MathF.PI));
                v.Add(R * MathF.Sin(u * MathF.Tau) * MathF.Sin(vv * MathF.PI));
            }
        }
        for (int y = 0; y < SegH; y++)
        {
            for (int x = 0; x < SegW; x++)
            {
                uint a = (uint)(y * (SegW + 1) + x), b = a + SegW + 1;
                idx.AddRange([a, b, a + 1, b, b + 1, a + 1]);
            }
        }
        _skyVao = _own.PositionMesh([.. v], [.. idx]).Vao;
        _skyCount = (uint)idx.Count;
    }

    /// <summary>
    /// Makes the water's mesh: a flat square 8 km wide at the area's water level, centred on the area.
    /// </summary>
    private void BuildWater(WorldScene s)
    {
        float half = 4000, y = s.Water;
        _waterVao = _own.PositionMesh([-half, y, -half, half, y, -half, half, y, half, -half, y, half], [0, 2, 1, 0, 3, 2]).Vao;
    }
}
