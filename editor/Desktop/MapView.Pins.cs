using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// Search results and players over the map: pins, the chosen one with a ring, and the players' discs.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>What the pins' shapes were last made for: the pins, the chosen one, the zoom and the players.</summary>
    private (IReadOnlyList<Vector2>? Pins, int Chosen, float Mpp, IReadOnlyList<(string, float, float)>? Players) _pinsKey;

    /// <summary>The pins' shapes last made (<see cref="PinShapes"/>).</summary>
    private float[] _pinShapes = [];

    /// <summary>
    /// Search results (orange pins, the chosen one larger with a ring) and, live, the players (blue).
    /// </summary>
    private void DrawPins() => Fill(PinShapes(), PrimitiveType.Triangles);

    /// <summary>
    /// The shapes of <c>DrawPins</c>, as triangles (x, z then RGBA per corner): made again only when the pins, the
    /// chosen one, the zoom or the players changed (the pins and players are copies made when set, so a new one is a
    /// change), else the last ones as they are.
    /// </summary>
    internal float[] PinShapes()
    {
        float m = MetersPerPixel;
        var key = (Pins, PinChosen, m, Players);
        if (_pinsKey == key)
        {
            return _pinShapes;
        }
        _pinsKey = key;
        bool chosen = PinChosen >= 0 && PinChosen < Pins.Count;
        // Corners: 2 discs of 8 sides a pin; the chosen one 24 quads and 2 discs of 12; 2 discs of 12 a player.
        var data = new List<float>(6 * ((Pins.Count - (chosen ? 1 : 0)) * 48 + (chosen ? 216 : 0) + Players.Count * 72));
        var dark = new Vector4(30 / 255f, 15 / 255f, 5 / 255f, 0.9f);
        for (int i = 0; i < Pins.Count; i++)
        {
            if (i == PinChosen)
            {
                continue;
            }
            Disc(data, Pins[i].X, Pins[i].Y, 4.5f * m, dark, 8);
            Disc(data, Pins[i].X, Pins[i].Y, 3.5f * m, new Vector4(1, 138 / 255f, 42 / 255f, 1), 8);
        }
        if (chosen)
        {
            var p = Pins[PinChosen];
            var gold = new Vector4(1, 210 / 255f, 122 / 255f, 1);
            for (int i = 0; i < 24; i++)
            {
                // The ring: a thin band of quads.
                float a0 = i * MathF.Tau / 24, a1 = (i + 1) * MathF.Tau / 24, r0 = 12.5f * m, r1 = 14 * m;
                Quad(data, new(p.X + r0 * MathF.Cos(a0), p.Y + r0 * MathF.Sin(a0)), new(p.X + r1 * MathF.Cos(a0), p.Y + r1 * MathF.Sin(a0)),
                    new(p.X + r1 * MathF.Cos(a1), p.Y + r1 * MathF.Sin(a1)), new(p.X + r0 * MathF.Cos(a1), p.Y + r0 * MathF.Sin(a1)), gold);
            }
            Disc(data, p.X, p.Y, 7.5f * m, dark);
            Disc(data, p.X, p.Y, 6 * m, gold);
        }
        foreach (var (_, x, z) in Players)
        {
            Disc(data, x, z, 8 * m, new Vector4(11 / 255f, 32 / 255f, 48 / 255f, 1));
            Disc(data, x, z, 6 * m, new Vector4(79 / 255f, 195 / 255f, 1, 1));
        }
        _pinShapes = [.. data];
        return _pinShapes;
    }
}
