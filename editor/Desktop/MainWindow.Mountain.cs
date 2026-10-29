using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The Mountain tool's click: the mountain goes into the ground there (grid point), then the biome's trees and
    /// rocks grow on it (a second undo step).
    /// </summary>
    internal async Task PutMountain(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var m = MountainPanel.Spec;
        var preset = MountainPanel.Preset;
        if (s.Mountain(gx, gz, m, MountainPanel.Clear, Lang.T("mountain.undo.placed", preset.Shown)) is not { } done)
        {
            Msg(MountainTooBig(m.Kind, m.Reach));
            return;
        }
        UpdateSaveBar();
        var placed = MountainPlaced(m.Kind, m.Height, done.Cleared > 0 ? Said.Of("mountain.msg.buried", done.Cleared) : Said.Empty);
        Msg(placed);
        if (!MountainPanel.Grow)
        {
            Msg(placed + Said.Of("mountain.msg.undoDiscs"));
            return;
        }
        var shape = Mountain.Shape(m);
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ, wx = ox + gx, wz = oz + gz;
        // The spots are judged on the area as it is now: what grows is only put in if it is still the same.
        int generation = s.Generation;
        // On the slopes it raised by more than a metre (not around its foot, where the old ones stand).
        var spots = await Growth.Spots(s, wx - m.Reach, wz - m.Reach, wx + m.Reach, wz + m.Reach, o => shape(o.X - wx, o.Z - wz) > 1,
            NameOfPrefab, t => Msg(placed + t), maxZones: 49);
        if (Session != s || s.Generation != generation)
        {
            // Another area opened (Follow grew it), or the ground changed (an undo, a stroke, another mountain): the
            // trees would go to an area no longer shown, or float or be buried.
            Msg(placed + Said.Of("mountain.msg.areaChanged"));
            UpdateSaveBar();
            return;
        }
        if (spots is { Count: > 0 })
        {
            Growth.Commit(s, Lang.T("mountain.undo.grew", spots.Count), spots);
            placed += Said.Of("mountain.msg.grew", spots.Count);
            Msg(placed + Said.Of("mountain.msg.undoTwice"));
        }
        else
        {
            // Nothing grew: one step to take back. When growing could not be done, why (Spots said it) stays.
            Msg((spots == null ? LastSaid : placed) + Said.Of("mountain.msg.undoDiscs"));
        }
        UpdateSaveBar();
    }

    /// <summary>The message for a mountain that does not fit in the open area: one whole sentence per kind.</summary>
    /// <param name="kind">The mountain's kind.</param>
    /// <param name="reach">How far it reaches around the click, in metres.</param>
    /// <exception cref="ArgumentOutOfRangeException">An unknown kind.</exception>
    private static Said MountainTooBig(MountainKind kind, float reach) => kind switch
    {
        MountainKind.Peak => Said.Of("mountain.msg.tooBig.peak", reach),
        MountainKind.Ridge => Said.Of("mountain.msg.tooBig.ridge", reach),
        MountainKind.Range => Said.Of("mountain.msg.tooBig.range", reach),
        MountainKind.Mesa => Said.Of("mountain.msg.tooBig.mesa", reach),
        MountainKind.Volcano => Said.Of("mountain.msg.tooBig.volcano", reach),
        MountainKind.Hills => Said.Of("mountain.msg.tooBig.hills", reach),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such mountain kind."),
    };

    /// <summary>The message for a mountain put in: one whole sentence per kind.</summary>
    /// <param name="kind">The mountain's kind.</param>
    /// <param name="height">Its height, in metres.</param>
    /// <param name="buried">The clause on the trees and rocks it buried, or <c>Said.Empty</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">An unknown kind.</exception>
    private static Said MountainPlaced(MountainKind kind, float height, Said buried) => kind switch
    {
        MountainKind.Peak => Said.Of("mountain.msg.placed.peak", height, buried),
        MountainKind.Ridge => Said.Of("mountain.msg.placed.ridge", height, buried),
        MountainKind.Range => Said.Of("mountain.msg.placed.range", height, buried),
        MountainKind.Mesa => Said.Of("mountain.msg.placed.mesa", height, buried),
        MountainKind.Volcano => Said.Of("mountain.msg.placed.volcano", height, buried),
        MountainKind.Hills => Said.Of("mountain.msg.placed.hills", height, buried),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such mountain kind."),
    };

    /// <summary>
    /// The Shape tool's click: the shape goes into the ground there (grid point).
    /// </summary>
    internal void PutShape(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        if (ShapePanel.Function is not { } f)
        {
            Msg("shape.msg.fixFirst");
            return;
        }
        var (touched, clamped, bad) = s.Shape(gx, gz, f, ShapePanel.Radius, ShapePanel.Height, ShapePanel.Label);
        Msg(touched == 0 ? Said.Of("shape.msg.nothing") + (bad > 0 ? Said.Of("shape.msg.noNumber", bad) : Said.Empty)
            : clamped ? Said.Of("shape.msg.clamped")
            : Said.Of("shape.msg.placed", touched));
        UpdateSaveBar();
    }
}
