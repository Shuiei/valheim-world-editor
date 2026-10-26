using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// Pasted copies of the mod's pieces made pieces of their own. A train's wagons and a flatbed's deck are ties the
/// manifest gives (a pasted train keeps the copy's own wagons, a pasted flatbed what was copied on its deck, each with
/// new ids); what depends on where a copy lands (the track a vehicle stands on, a sign's stop, track's exact turn) is
/// set again by <see cref="RailPlacer"/> once it is pasted. Here what is left:
/// <list type="bullet">
/// <item>a vehicle loses its driver, levers and trip, and the hand cart who held its handles;</item>
/// <item>a train keeps its name but loses its number: the server gives it one of its own, as to a train just built (the
/// number is what an unnamed train goes by, and the server never numbers a train again);</item>
/// <item>a piece no longer on a deck (its flatbed was not copied) loses its place on that deck too;</item>
/// <item>the text the mod's first versions kept the lines and wagons in (it still reads it in older saves) goes: it names
/// the originals' track and wagons.</item>
/// </list>
/// </summary>
public sealed class PastedCopies : ICopyRewriter
{
    /// <summary>Every key a copy has rewritten or taken away: the copy rewriter handles objects holding one.</summary>
    public static readonly string[] Keys =
    [
        RailroadsMod.Keys.Line, RailroadsMod.Keys.At, RailroadsMod.Keys.Dir, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route,
        RailroadsMod.Keys.After, RailroadsMod.Keys.Legs, RailroadsMod.Keys.Moving, RailroadsMod.Keys.Speed, RailroadsMod.Keys.Driver,
        RailroadsMod.Keys.Destination, RailroadsMod.Keys.Switch, RailroadsMod.Keys.Gear, RailroadsMod.Keys.Way, RailroadsMod.Keys.Stop,
        RailroadsMod.Keys.Front, RailroadsMod.Keys.Back, RailroadsMod.Keys.DeckPos, RailroadsMod.Keys.DeckRot, RailroadsMod.Keys.TrainNumber,
    ];

    /// <summary>The lines a vehicle keeps (bytes now; text in the mod's first versions): set again where it lands.</summary>
    private static readonly string[] Lines = [RailroadsMod.Keys.Line, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route, RailroadsMod.Keys.After];

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        foreach (string key in Lines)
        {
            copy.SetBytes(key, null);
            copy.SetString(key, null);
        }
        // The wagons and the wagon's own id as text (the mod's first versions): the bytes are the train's tie.
        copy.SetString(RailroadsMod.Keys.Wagons, null);
        copy.SetString(RailroadsMod.Keys.Self, null);
        copy.SetBytes(RailroadsMod.Keys.Legs, null);
        // A locomotive's stop along its route, a sign's on the track: set again where the copy lands.
        copy.SetFloat(RailroadsMod.Keys.Stop, null);
        copy.SetVector3(RailroadsMod.Keys.Stop, null);
        copy.SetFloat(RailroadsMod.Keys.At, null);
        copy.SetFloat(RailroadsMod.Keys.Speed, null);
        foreach (string key in new[]
        {
            RailroadsMod.Keys.Dir, RailroadsMod.Keys.Moving, RailroadsMod.Keys.Switch, RailroadsMod.Keys.Gear, RailroadsMod.Keys.Way,
            RailroadsMod.Keys.TrainNumber,
        })
        {
            copy.SetInt(key, null);
        }
        foreach (string key in new[] { RailroadsMod.Keys.Driver, RailroadsMod.Keys.Front, RailroadsMod.Keys.Back })
        {
            copy.SetLong(key, null);
        }
        copy.SetString(RailroadsMod.Keys.Destination, null);
        if (!copy.TryGetLong(RailroadsMod.Keys.Deck, out _))
        {
            copy.SetVector3(RailroadsMod.Keys.DeckPos, null);
            copy.SetQuaternion(RailroadsMod.Keys.DeckRot, null);
        }
    }
}
