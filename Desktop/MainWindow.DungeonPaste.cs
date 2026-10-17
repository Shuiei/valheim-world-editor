using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// A dungeon blueprint pasted (the Paste tool's click, the clipboard's copy tagged as a dungeon): its pieces into the
    /// world above the ground at the grid point clicked, behind the entrance of the biome there (<see cref="PlaceDungeon"/>),
    /// turned and mirrored as the Paste tool shows them (its height offset does not apply: the dungeon's height is the
    /// game's). The first portal among its pieces marks where a player arrives, facing its way, and is left out (the
    /// entrance's door out stands there); without one, a player arrives at the blueprint's middle, facing north.
    /// </summary>
    /// <param name="clip">The copy on the clipboard.</param>
    /// <param name="at">The grid point clicked.</param>
    internal void PasteDungeon(CopyData clip, Vector2 at)
    {
        if (Session is not { } s || View.Scene is not { } scene)
        {
            return;
        }
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        var ground = GroundAt(scene, ox + at.X, oz + at.Y);
        // Turned and mirrored as the Paste tool pastes (PasteTool.Apply).
        var paste = View.Paste;
        Vector3 Place(CopyData.Obj o)
        {
            var p = paste.Xf(new Vector2(o.Dx, o.Dz));
            return new Vector3(p.X, o.Dy, p.Y);
        }
        int markerAt = clip.Objects.FindIndex(o => o.Name == "portal_wood");
        var marker = markerAt >= 0 ? clip.Objects[markerAt] : null;
        var pieces = clip.Objects.Where((o, i) => i != markerAt)
            .Select(o => new DungeonPiece(o.Prefab, Place(o), BlueprintFormats.FromEuler(paste.Rotated(o.Rotation)), o.Scale, o.Data, Terrain.PieceCatalog.Get(o.Prefab)?.Tool != null)).ToList();
        // Counted as placing counts them: only what this world can make.
        if (!pieces.Exists(p => CanMake(scene.World, p.Prefab)))
        {
            Msg(pieces.Count == 0 && marker != null ? Said.Of("dungeon.msg.onlyPortal") : Said.Of("dungeon.msg.noPiece"));
            return;
        }
        string biome = BiomeName(scene.Terrain?.BiomeAt(ground.X, ground.Z));
        string name = clip.Name ?? "dungeon";
        PlaceDungeon(scene, s, ground, new DungeonToPlace(name, true, biome, PortalTag(StableHash.Of(name)), pieces,
            marker == null ? Vector3.Zero : Place(marker), marker == null ? 0f : paste.Rotated(marker.Rotation).Y, null));
    }

    /// <summary>Whether this world can make a prefab (its model or its details are known): placing leaves out the rest.</summary>
    private static bool CanMake(WorldSave world, int prefab) => world.ModelFor(prefab, null) != null || Terrain.PrefabCatalog.Details(prefab) != null;

    /// <summary>The biome's name as the dungeon generator knows it (the Black Forest's for none, or the sea's).</summary>
    private static string BiomeName(ValheimGen.Heightmap.Biome? biome) => biome switch
    {
        ValheimGen.Heightmap.Biome.Meadows => "Meadows",
        ValheimGen.Heightmap.Biome.Swamp => "Swamp",
        ValheimGen.Heightmap.Biome.Mountain => "Mountain",
        ValheimGen.Heightmap.Biome.Plains => "Plains",
        ValheimGen.Heightmap.Biome.Mistlands => "Mistlands",
        ValheimGen.Heightmap.Biome.AshLands => "Ashlands",
        ValheimGen.Heightmap.Biome.DeepNorth => "Deep North",
        _ => "Black Forest",
    };

    /// <summary>A point of the area on the ground (or the water): world x, z at the ground's height there.</summary>
    private static Vector3 GroundAt(WorldScene scene, float x, float z)
    {
        int i = Math.Clamp((int)MathF.Round(x - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1), j = Math.Clamp((int)MathF.Round(z - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
        return new Vector3(x, Math.Max(scene.Heights[j * scene.W + i], scene.Water), z);
    }
}
