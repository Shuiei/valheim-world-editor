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
        var plan = Uplift.Make(World, Terrain, Edits, NextId);
        if (plan == null)
        {
            return null;
        }
        var before = Edits.Mark();
        var modifiers = Modifiers;
        Uplift.Apply(plan, Edits);
        UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        return new Lifting(plan, before, modifiers, Edits.Version);
    }
}
