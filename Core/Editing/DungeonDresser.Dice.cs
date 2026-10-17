using System.Numerics;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>Moves a wall spot in by depth metres.</summary>
    private static (float U, float V) In((float U, float V, float Yaw) s, float depth) =>
        (s.U + depth * MathF.Sin(s.Yaw * MathF.PI / 180), s.V + depth * MathF.Cos(s.Yaw * MathF.PI / 180));

    /// <summary>A random number from a to b.</summary>
    private float R(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

    /// <summary>One of the list at random.</summary>
    private T One<T>(T[] list) => list[_rnd.Next(list.Length)];

    /// <summary>One of the names at random.</summary>
    private string One(params string[] list) => list[_rnd.Next(list.Length)];

    /// <summary>A statue: one of <paramref name="whole"/>, or a broken one in a decayed dungeon.</summary>
    private string Statue(params string[] whole) => _decay > 0.4f ? One("StatueThor_broken_top", "StatueFreya_broken_left") : One(whole);

    /// <summary>A pair of statues (Freya's and Thor's), broken in a decayed dungeon.</summary>
    private string[] Statues() => _decay > 0.4f ? ["StatueFreya_broken_left", "StatueThor_broken_top"] : ["StatueFreya", "StatueThor"];

    /// <summary>
    /// A column lying on the floor at (u, v), its length along <paramref name="yaw"/> (degrees, the room's frame); it
    /// takes the floor within <paramref name="room"/> metres.
    /// </summary>
    private void FallenPillar(Frame f, string prefab, float u, float v, float yaw, float room)
    {
        var q = DungeonOut.Yaw(f.Yaw(yaw)) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
        _o.Add(prefab, f.W(u, v, f.F + 0.55f), q);
        f.Take(u, v, room);
    }

    /// <summary>One of the names at random, not the one just used when there is a choice (repeated detail is what the eye tires of first).</summary>
    private string Another(string[] list, string last)
    {
        int k = _rnd.Next(list.Length);
        return list.Length > 1 && list[k] == last ? list[(k + 1) % list.Length] : list[k];
    }

    /// <summary>True with chance p (0 to 1).</summary>
    private bool Chance(double p) => _rnd.NextDouble() < p;

    /// <summary>n as a whole count: its whole part, plus one with the chance of its fraction (0 for n &lt;= 0).</summary>
    private int Count(float n) => n <= 0 ? 0 : (int)MathF.Floor(n) + (_rnd.NextDouble() < n - MathF.Floor(n) ? 1 : 0);
}
