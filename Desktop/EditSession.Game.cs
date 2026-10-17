
namespace TerrainEditor.Desktop;

/// <summary>
/// Live: what the game changed, taken into the open area without an undo step.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Live: what the game changed (WorldSession.MergeGame), taken into the area without an undo step: objects removed
    /// in the game go, changed ones are shown as they are now (same thing, same place in the history), new ones come,
    /// and the ground of the zones given is read again. Taking the same changes twice changes nothing (an area opened
    /// meanwhile may already have them). Not during a stroke (false: given again next time).
    /// </summary>
    /// <returns>False during a stroke: given again next time.</returns>
    public bool TakeGameChanges(WorldSession.Followed f)
    {
        var changed = new List<int>();
        bool ground = f.Ground.Count > 0 || f.Merged.Modifiers;
        lock (_lock)
        {
            if (_stroke != null)
            {
                return false;
            }
            var gone = f.Merged.Vanished.Concat(f.Ours).ToHashSet();
            var update = f.Merged.Updated.Concat(f.Merged.Added).ToHashSet();
            // The objects as the world has them now, by id (those changed or new).
            var now = WorldScene.FindThings(Scene.World, Edits, update);
            TakeThingChanges(gone, now, changed);
            if (ground)
            {
                ReadGround(f.Merged.Modifiers);
            }
        }
        if (changed.Count > 0 || ground)
        {
            // What was worked out from the area before (an open inspector, a running script) is out of date.
            Interlocked.Increment(ref _generation);
        }
        if (changed.Count > 0)
        {
            // A group's root moved in the game: its parts are drawn where it is now.
            Notify(changed);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Whether a world position (view space) is inside the area's zones.
    /// </summary>
    /// <param name="p">The position.</param>
    internal bool InsideArea(System.Numerics.Vector3 p) =>
        p.X >= Scene.OriginX && p.X < (Scene.X0 + Scene.ZonesX - 1) * 64f + 32f
        && p.Z >= Scene.OriginZ && p.Z < (Scene.Z0 + Scene.ZonesZ - 1) * 64f + 32f;

    /// <summary>
    /// Reads the area's ground again over the whole area: the generated base and its discs from the world (when
    /// baseToo), and the edits.
    /// </summary>
    /// <param name="baseToo">Whether the generated ground is read again from the world (its discs or modifiers changed).</param>
    internal void ReadGround(bool baseToo)
    {
        if (baseToo && Scene.Owner is { } owner)
        {
            Scene.Modifiers = owner.Modifiers;
            Ground.ReadBase(owner.Terrain);
        }
        Ground.TakeEdits(Edits);
        Touch((0, 0, Ground.W - 1, Ground.H - 1));
    }
}
