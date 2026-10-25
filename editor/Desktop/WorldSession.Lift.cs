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
    private void KeepLiftForm(Lifting lift, IReadOnlyCollection<ZoneReset> reset)
    {
        var groundReset = reset.Where(r => r.Ground).Select(r => (r.X, r.Z)).ToHashSet();
        foreach (var z in lift.Plan.Zones.Where(z => !groundReset.Contains((z.ZoneX, z.ZoneZ))))
        {
            var key = (z.ZoneX, z.ZoneZ);
            var e = lift.Before.Zones.TryGetValue(key, out var was) ? was.Clone() : new ZoneEdit(z.ZoneX, z.ZoneZ);
            float[] before = lift.Bases[key], now = Terrain.BaseZone(z.ZoneX, z.ZoneZ);
            for (int k = 0; k < EditStore.Cells; k++)
            {
                e.Lift[k] = EditStore.KeepLift(e.Lift[k] + before[k] - now[k]);
            }
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
        // Live, the world's objects stand on the ground as last sent to the game (the world is not read again after it),
        // and No limit ground the game has as discs already is not worked out again.
        var plan = IsLive ? Uplift.Make(World, Terrain, Edits, NextId, Edits.BaselineOf, Edits.AsPlanned) : Uplift.Make(World, Terrain, Edits, NextId);
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
}
