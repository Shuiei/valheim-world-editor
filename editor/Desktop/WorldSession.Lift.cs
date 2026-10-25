using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// A save or apply that wrote or sent nothing: the No limit ground it turned into discs is put back as it was,
    /// unless the edits changed since (a stroke drawn while the game answered).
    /// </summary>
    /// <returns>Put back.</returns>
    private bool Unlift(Lifting lift)
    {
        if (Edits.Version != lift.Version)
        {
            return false;
        }
        Edits.Restore(lift.Before);
        UseModifiers(lift.Modifiers);
        return true;
    }

    /// <summary>
    /// Once the ground discs are in the game (live), or in the files and the world read again (a save that reset zones):
    /// the session goes back to its No limit ground over them (put onto the generated ground with the discs, so it shows
    /// as it did), not a change while it stays so (EditStore.PutPlanned). Its history then puts back exactly the ground
    /// it changed, the ground round the discs too, and the next Apply live works the discs out again only where that
    /// ground changed. The history file keeps it (see <see cref="DiscZones"/>).
    /// </summary>
    /// <param name="lift">The No limit ground made into discs.</param>
    /// <param name="reset">Zones reset meanwhile: one whose ground went with it keeps what the game has.</param>
    /// <param name="read">
    /// The edits were read again from the world (zone resets); otherwise a zone changed while the discs went out (a
    /// stroke ended meanwhile) keeps that change, over the discs, to send next.
    /// </param>
    private void KeepLiftForm(Lifting lift, IReadOnlyCollection<ZoneReset> reset, bool read)
    {
        var groundReset = reset.Where(r => r.Ground).Select(r => (r.X, r.Z)).ToHashSet();
        foreach (var z in lift.Plan.Zones.Where(z => !groundReset.Contains((z.ZoneX, z.ZoneZ))))
        {
            var key = (z.ZoneX, z.ZoneZ);
            if (!read && Edits.Get(z.ZoneX, z.ZoneZ)?.Fingerprint() != z.Fingerprint())
            {
                continue;
            }
            var e = lift.Before.Zones.TryGetValue(key, out var was) ? was.Clone() : new ZoneEdit(z.ZoneX, z.ZoneZ);
            e.Rebase(lift.Bases[key], Terrain.BaseZone(z.ZoneX, z.ZoneZ));
            Edits.PutPlanned(e);
            _discZones[key] = HistoryFile.Written(z);
        }
    }

    /// <summary>Switches the session and its generator to new flattening modifiers.</summary>
    private void UseModifiers(TerrainModifiers modifiers)
    {
        Modifiers = modifiers;
        Terrain.UseModifiers(modifiers);
    }

    /// <summary>
    /// No limit ground waiting (lifts): turned into ground discs and ordinary edits (Uplift), the ground
    /// worked out again with them. Null when there was none.
    /// </summary>
    private Lifting? LiftToDiscs()
    {
        // Live, the world's objects stand on the ground as last sent to the game (the world is not read again after it).
        // No limit ground the world holds as discs already (applied live, or in the save it was read from) is not worked
        // out again.
        var plan = Uplift.Make(World, Terrain, Edits, NextId, IsLive ? Edits.BaselineOf : null, Edits.AsPlanned);
        if (plan == null)
        {
            return null;
        }
        var before = Edits.Mark();
        var modifiers = Modifiers;
        var bases = plan.Zones.ToDictionary(z => (z.ZoneX, z.ZoneZ), z => (float[])Terrain.BaseZone(z.ZoneX, z.ZoneZ).Clone());
        Uplift.Apply(plan, Edits);
        UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        return new Lifting(plan, before, modifiers, Edits.Version, bases);
    }

    /// <summary>
    /// The generated ground at a ground point (world metres) as it was before No limit ground became discs: the
    /// plan's zones as they were, the others as they are (the discs reach no further).
    /// </summary>
    private Func<int, int, float> BasesBefore(Lifting lift) => HistoryFile.BaseHeights(this, lift.Bases);
}
