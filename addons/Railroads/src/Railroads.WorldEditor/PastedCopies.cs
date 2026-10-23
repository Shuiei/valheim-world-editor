using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// Pasted copies of the mod's pieces, made to stand where they are pasted and to be pieces of their own. The mod keeps
/// world places and the game's object ids in its data, which name the originals:
/// <list type="bullet">
/// <item>a track piece (or old station) takes the turn it is pasted with as its exact turn, which the mod gives it back
/// as the world loads (the original's would turn a turned copy back);</item>
/// <item>a vehicle loses its place on the original's track, its trip, its wagons and who drives it: the mod puts it on
/// the track nearest where it stands (within 1.5 m) the first time a player comes near, as for one just built; a wagon
/// that named itself (the mod's way of finding its wagons again) no longer does, else the mod would hand it the
/// original's place in its train;</item>
/// <item>a station sign loses its stop, a point on the original's track: until it is built again with the hammer, the
/// copy is no stop;</item>
/// <item>a flatbed loses its deck's id (the mod gives it a new one), and a piece built on a deck becomes a piece of its
/// own, where it is pasted (with its deck's id it would be carried back to the original flatbed).</item>
/// </list>
/// </summary>
public sealed class PastedCopies : ICopyRewriter
{
    /// <summary>Every key a copy has rewritten or taken away: the copy rewriter handles objects holding one.</summary>
    public static readonly string[] Keys =
    [
        RailroadsMod.Keys.Turn, RailroadsMod.Keys.Stop,
        RailroadsMod.Keys.Line, RailroadsMod.Keys.At, RailroadsMod.Keys.Dir, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route,
        RailroadsMod.Keys.After, RailroadsMod.Keys.Legs, RailroadsMod.Keys.Moving, RailroadsMod.Keys.Wagons, RailroadsMod.Keys.Self,
        RailroadsMod.Keys.Speed, RailroadsMod.Keys.Driver, RailroadsMod.Keys.Destination, RailroadsMod.Keys.Switch,
        RailroadsMod.Keys.Gear, RailroadsMod.Keys.Way, RailroadsMod.Keys.Front, RailroadsMod.Keys.Back,
        RailroadsMod.Keys.DeckKey, RailroadsMod.Keys.Deck, RailroadsMod.Keys.DeckPos, RailroadsMod.Keys.DeckRot,
    ];

    /// <summary>
    /// The keys the mod first kept as text and now keeps as bytes (it still reads the text of older saves): both go.
    /// </summary>
    private static readonly string[] TextOrBytes =
    [
        RailroadsMod.Keys.Line, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route, RailroadsMod.Keys.After, RailroadsMod.Keys.Wagons,
        RailroadsMod.Keys.Self,
    ];

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        if (copy.TryGetQuaternion(RailroadsMod.Keys.Turn, out _))
        {
            copy.SetQuaternion(RailroadsMod.Keys.Turn, copy.Orientation);
        }
        // A locomotive's stop along its route, a sign's on the track.
        copy.SetFloat(RailroadsMod.Keys.Stop, null);
        copy.SetVector3(RailroadsMod.Keys.Stop, null);
        foreach (string key in TextOrBytes)
        {
            copy.SetBytes(key, null);
            copy.SetString(key, null);
        }
        copy.SetBytes(RailroadsMod.Keys.Legs, null);
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
