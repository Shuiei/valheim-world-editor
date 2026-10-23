using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// Pasted copies of the mod's pieces made pieces of their own: the mod names other objects by the game's object ids and
/// by ids of its own, which in a copy name the originals. What depends on where a copy lands (the track a vehicle
/// stands on, a sign's stop, track's exact turn) is set again by <see cref="RailPlacer"/> once it is pasted.
/// <list type="bullet">
/// <item>a vehicle loses the original's wagons, driver, levers and trip, and the hand cart who held its handles;</item>
/// <item>a wagon that named itself (the mod's way of finding its wagons again) no longer does, else the mod would hand
/// it the original's place in its train;</item>
/// <item>a flatbed loses its deck's id (the mod gives it a new one), and a piece built on a deck becomes a piece of its
/// own, where it is pasted (with its deck's id it would be carried back to the original flatbed).</item>
/// </list>
/// </summary>
public sealed class PastedCopies : ICopyRewriter
{
    /// <summary>Every key a copy has taken away: the copy rewriter handles objects holding one.</summary>
    public static readonly string[] Keys =
    [
        RailroadsMod.Keys.Line, RailroadsMod.Keys.At, RailroadsMod.Keys.Dir, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route,
        RailroadsMod.Keys.After, RailroadsMod.Keys.Legs, RailroadsMod.Keys.Moving, RailroadsMod.Keys.Wagons, RailroadsMod.Keys.Self,
        RailroadsMod.Keys.Speed, RailroadsMod.Keys.Driver, RailroadsMod.Keys.Destination, RailroadsMod.Keys.Switch,
        RailroadsMod.Keys.Gear, RailroadsMod.Keys.Way, RailroadsMod.Keys.Stop, RailroadsMod.Keys.Front, RailroadsMod.Keys.Back,
        RailroadsMod.Keys.DeckKey, RailroadsMod.Keys.Deck, RailroadsMod.Keys.DeckPos, RailroadsMod.Keys.DeckRot,
    ];

    /// <summary>
    /// The keys the mod first kept as text and now keeps as bytes (it still reads the text of older saves): both go.
    /// </summary>
    private static readonly string[] TextOrBytes =
        [RailroadsMod.Keys.Line, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route, RailroadsMod.Keys.After, RailroadsMod.Keys.Wagons, RailroadsMod.Keys.Self];

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        foreach (string key in TextOrBytes)
        {
            copy.SetBytes(key, null);
            copy.SetString(key, null);
        }
        copy.SetBytes(RailroadsMod.Keys.Legs, null);
        // A locomotive's stop along its route, a sign's on the track: set again where the copy lands.
        copy.SetFloat(RailroadsMod.Keys.Stop, null);
        copy.SetVector3(RailroadsMod.Keys.Stop, null);
        copy.SetFloat(RailroadsMod.Keys.At, null);
        copy.SetFloat(RailroadsMod.Keys.Speed, null);
        foreach (string key in new[] { RailroadsMod.Keys.Dir, RailroadsMod.Keys.Moving, RailroadsMod.Keys.Switch, RailroadsMod.Keys.Gear, RailroadsMod.Keys.Way })
        {
            copy.SetInt(key, null);
        }
        foreach (string key in new[] { RailroadsMod.Keys.Driver, RailroadsMod.Keys.Front, RailroadsMod.Keys.Back, RailroadsMod.Keys.DeckKey, RailroadsMod.Keys.Deck })
        {
            copy.SetLong(key, null);
        }
        copy.SetString(RailroadsMod.Keys.Destination, null);
        copy.SetVector3(RailroadsMod.Keys.DeckPos, null);
        copy.SetQuaternion(RailroadsMod.Keys.DeckRot, null);
    }
}
