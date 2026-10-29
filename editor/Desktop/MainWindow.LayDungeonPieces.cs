using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Adds the pieces the game knows how to make, each with its own data; the ones it does not are counted as skipped.
    /// </summary>
    /// <param name="l">The placement.</param>
    private static void AddPieces(DungeonLay l)
    {
        // Every object with its own data. Made fresh (nothing kept of the object it is copied from); the data goes on
        // when it is written (a generated dungeon's: no builder, so the game takes it for a ruin, as its own dungeons).
        int skipped = 0;
        foreach (var p in l.Pieces)
        {
            if (!l.Known(p.Prefab))
            {
                skipped++;
                continue;
            }
            l.Adds.Add((new NewObject(0, p.Prefab, p.Position + l.Origin, BlueprintFormats.ToEuler(p.Rotation), p.Scale, Data: p.Data), p.Piece));
        }
        l.Skipped = skipped;
    }

    /// <summary>
    /// The two portals of a dungeon laid without an entrance: one at the arrival, one in front of the ground, linked
    /// to each other and carrying the dungeon's tag.
    /// </summary>
    /// <param name="scene">The area.</param>
    /// <param name="l">The placement.</param>
    /// <param name="d">The dungeon.</param>
    private static void AddPortalPair(WorldScene scene, DungeonLay l, DungeonToPlace d)
    {
        // Two portals of the same tag, written linked (the game pairs only portals it sees made, or that
        // a save gives linked).
        int link = Random.Shared.Next(1, int.MaxValue);
        int portal = StableHash.Of("portal_wood");
        var portalData = new[] { ObjectField.NoBuilder, new ObjectField("strings", StableHash.Of("tag"), d.PortalTag) };
        foreach (var (where, yaw, kindByte) in new[] { (l.Arrival, l.PortalYaw, (byte)0x01), (GroundAt(scene, l.At.X, l.At.Z + 3), 180f, (byte)0x11) })
        {
            var n = new NewObject(0, portal, where, new Vector3(0, yaw, 0), 0);
            if (l.Blank(n) is not { } bytes)
            {
                continue;
            }
            // The link is no field of the data: set on the object itself.
            var z = ZdoData.Parse(bytes);
            z.Connection = [kindByte, .. BitConverter.GetBytes(link)];
            l.Adds.Add((n with { Raw = z.Serialize(), Fresh = false, Data = portalData }, true));
        }
    }
}
