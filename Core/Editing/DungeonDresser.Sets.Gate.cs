
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A wall across the corridor at its middle, 3 m high, with the style's door in the middle of it.</summary>
    private void CorridorGate(Frame f)
    {
        float v = R(-1, 1);
        // Not by a doorway, nor on a pilaster's band of wall (the door against its columns, the wall doubled).
        if (f.NearDoor(-f.HalfA, v, 3) || f.NearDoor(f.HalfA, v, 3) || _bands.Any(b => MathF.Abs(b - v) < _mat.Thick + 0.5f))
        {
            return;
        }
        // A corridor is 3 m wide within its walls: no room for jambs beside a 2 m door, so the gate is the door alone
        // under a band of wall from wall to wall (held by both; it may rise into the roof). The band runs on into the
        // walls to their middle: between their faces a thin material's width is no whole number of pieces, and the gap
        // left at one end let it fall.
        float into = f.HalfA + _mat.Thick / 2;
        InnerWall(f, -into, v, into, v, 3, f.H);
        string door = _style.Name switch
        {
            "Fortress" or "Temple" => "dungeon_forestcrypt_door",
            "Dvergr hold" => "dvergrtown_slidingdoor",
            _ => One("dungeon_sunkencrypt_irongate", "dungeon_sunkencrypt_irongate_rusty"),
        };
        _o.Add(door, f.W(0, v, f.F), f.Yaw(0));
        f.Take(0, v, 1.6f);
        f.Take(-(f.HalfA - 1), v, 1.2f);
        f.Take(f.HalfA - 1, v, 1.2f);
    }

    /// <summary>
    /// A collapse: a heap of rubble and broken wall against one side, a pillar fallen across half the way, stones
    /// about; the way stays open along the other side.
    /// </summary>
    private void CorridorCollapse(Frame f)
    {
        if (_decay <= 0.2f && _style.Name != "Ruins")
        {
            return;
        }
        // Decay's own stream: the Decay setting moves nothing else.
        var rnd = Local(f, 4);
        float Rr(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        string Pick(params string[] list) => list[rnd.Next(list.Length)];
        float v = Rr(-f.HalfD + 3, f.HalfD - 3);
        int side = rnd.Next(2) == 0 ? -1 : 1;
        if (f.NearDoor(side * f.HalfA, v, 3))
        {
            return;
        }
        float u = side * (f.HalfA - 1.1f);
        Put(f, Pick("Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble1"), u, v, Rr(0, 360), 1.0f, force: true);
        Put(f, Pick("stone_wall_2x1_ruin", "stone_wall_1x1_ruin"), u + side * 0.3f, v + 1.4f, Rr(-20, 20) + (side < 0 ? 90 : -90), 0.6f);
        Put(f, "Morkhalla_Rubble1", u - side * 0.6f, v - 1.5f, Rr(0, 360), 0.6f);
        if (_mat.Column[0].Prefab == "stone_pillar" && rnd.NextDouble() < 0.6)
        {
            FallenPillar(f, "stone_pillar", side * (f.HalfA - 2.1f), v - 0.4f, Rr(60, 120), 1.3f);
        }
        for (int i = 0; i < 3; i++)
        {
            Put(f, Pick("Pickable_StoneRock", "piece_pot1_cracked", "stone_wall_1x1_ruin", "Morkhalla_Rubble1"), u - side * Rr(0.8f, 2.2f), v + Rr(-2.4f, 2.4f), Rr(0, 360), 0.3f);
        }
    }
}
