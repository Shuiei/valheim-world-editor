using System.Numerics;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>
    /// The name shown for a kind of dungeon, in the language in use and in English (Claude's): the game's own for the
    /// known ones, else its prefab name without DG_ and split into words (the same in both).
    /// </summary>
    public static Said Describe(Dungeons.Kind k) => k.Name switch
    {
        "DG_Cave" => Said.Of("dungeon.kind.frostCave"),
        "DG_SunkenCrypt" => Said.Of("dungeon.kind.sunkenCrypt"),
        "DG_ForestCrypt" => Said.Of("dungeon.kind.burialChambers"),
        "DG_HalfBurried_ForestCrypt" => Said.Of("dungeon.kind.burialChambersHalf"),
        "DG_DvergrTown" => Said.Of("dungeon.kind.infestedMine"),
        "DG_DvergrBoss" => Said.Of("dungeon.kind.infestedCitadel"),
        "DG_Hildir_Cave" => Said.Of("dungeon.kind.howlingCavern"),
        "DG_Hildir_ForestCrypt" => Said.Of("dungeon.kind.smoulderingTomb"),
        "DG_Hildir_PlainsFortress" => Said.Of("dungeon.kind.sealedTower"),
        "DG_GoblinCamp" => Said.Of("dungeon.kind.fulingVillage"),
        "DG_MeadowsVillage" => Said.Of("dungeon.kind.meadowsVillage"),
        "DG_MeadowsFarm" => Said.Of("dungeon.kind.meadowsFarm"),
        _ => Said.Raw(Words().Replace(k.Name.Replace("DG_", "", StringComparison.Ordinal).Replace('_', ' '), " $1").Trim()),
    };

    /// <summary>A capital letter after a small one: where <see cref="Describe"/> puts a space.</summary>
    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex Words();

    // ---- From building pieces.
    /// <summary>Plans the dungeon, builds its shell and dresses it, and draws its map.</summary>
    private static Result MakePieces(Settings s)
    {
        var rnd = new Random(s.Seed);
        var biome = BiomeOf(s.Biome);
        var style = StyleOf(s.Style ?? biome.Style);
        var mat = MaterialOf(s.Walls ?? biome.Material);
        string name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, style.Name) : s.Name!;
        var plan = DungeonPlanner.Make(s, style, rnd);
        Floors(plan, mat, s, rnd);
        var o = new DungeonOut();
        DungeonShell.Build(plan, mat, o, s);
        DungeonDresser.Dress(plan, mat, biome, style, s, rnd, o, name);
        var e = plan.Entrance;
        // On the entrance's floor finish (marble tiles, boards), as what is set on its floor stands.
        var arrival = new Vector3(e.CenterX, new DungeonDresser.Frame(e, mat, plan.Levels[e.Level]).Top + DungeonDresser.Lift, 2 * e.Z0 + 1.7f);
        var map = plan.Spaces.Select(sp => new PlanRoom(sp.Name, sp.Level, DungeonPlan.FloorY(sp.Level), 2 * sp.X0, 2 * sp.Z0, 2 * (sp.X0 + sp.W), 2 * (sp.Z0 + sp.H),
            sp.Corridor, sp.Main, sp.KeyChest, sp.Shape == DungeonPlan.Shape.Rect ? null : Runs(sp),
            sp.Recesses.Count == 0 ? null : sp.Recesses.Select(n => (2f * n.Tiles.Min(t => t.I), 2f * n.Tiles.Min(t => t.J), 2f * (n.Tiles.Max(t => t.I) + 1), 2f * (n.Tiles.Max(t => t.J) + 1))).ToList())).ToList();
        var index = plan.Spaces.Select((sp, i) => (sp, i)).ToDictionary(p => p.sp, p => p.i);
        var doors = plan.Doors.Select(d => new PlanDoor(d.A.Level, d.Center.X, d.Center.Z, d.Kind.ToString(), index[d.A], index[d.B])).ToList();
        foreach (var foot in plan.Spaces.Where(sp => sp.Room == Room.StairBottom && sp.Parent != null))
        {
            doors.Add(new PlanDoor(foot.Level, foot.CenterX, foot.CenterZ, "Stairs", index[foot.Parent!], index[foot]));
        }
        var notes = new List<Said>(plan.Notes);
        var key = plan.Spaces.FirstOrDefault(sp => sp.KeyChest);
        // The style's, the material's and the room's names are the generator's own (English in every language).
        string styleName = style.Name.ToLowerInvariant(), matName = mat.Name.ToLowerInvariant();
        int levels = plan.Levels.Count, roomCount = plan.Spaces.Count(sp => sp.IsRoom);
        notes.Insert(0, key != null
            ? Said.Of("dungeon.msg.madePiecesKey", name, styleName, matName, levels, roomCount, o.Items.Count, key.Name.ToLowerInvariant(), key.Level + 1)
            : Said.Of("dungeon.msg.madePieces", name, styleName, matName, levels, roomCount, o.Items.Count));
        return new Result(o.Items, arrival, null, null, notes, name, map, doors);
    }
}
