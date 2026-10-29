using System.Numerics;
using ValheimWorldEditor.Sdk;
using Keys = Railroads.WorldEditor.RailroadsMod.Keys;

namespace Railroads.WorldEditor;

/// <summary>
/// What the mod works out from where its pieces stand, set again wherever the editor places, pastes, moves or turns
/// them (the mod itself works it out only when a player builds them with the hammer, or as the world loads):
/// <list type="bullet">
/// <item>track and the old station keep their turn as their exact turn (<see cref="Keys.Turn"/>), which the mod gives
/// them back as the world loads: without it they would turn back;</item>
/// <item>a vehicle stands on the line of track nearest it, within 1.5 m, at that point of it and facing its way
/// (<see cref="Keys.Line"/>, <see cref="Keys.At"/>, <see cref="Keys.Dir"/>), as the mod puts a vehicle just built; with
/// none that near, on none (the mod puts it on track when a player first comes near). It stops: its trip, the lines its
/// wagons stood on, its speed and who sent it go, as when the mod ends a trip;</item>
/// <item>a station sign stops trains at the nearest point of straight track or of a loop's circle within 10 m
/// (<see cref="Keys.Stop"/>), as the mod sets it when the sign is built; with none, it is no stop.</item>
/// </list>
/// Track placed in the same step counts (a station pasted with its track), track the step moves away does not.
/// </summary>
/// <param name="models">The mod's models, for the track's lines.</param>
public sealed class RailPlacer(ModelLibrary models) : IObjectPlacer
{
    /// <summary>How far from track the mod puts a vehicle on it, in metres.</summary>
    public const float VehicleReach = 1.5f;

    /// <summary>How far from track a station sign finds its stop, in metres.</summary>
    public const float SignReach = 10;

    /// <summary>Every prefab placed: the mod's pieces.</summary>
    public static string[] Prefabs => [.. RailroadsMod.Pieces.Select(p => p.Prefab)];

    /// <summary>A sent train's trip and the lines its wagons stood on: they go when it is put somewhere else.</summary>
    private static readonly string[] Trip = [Keys.Trail, Keys.Route, Keys.After, Keys.Legs];

    /// <inheritdoc/>
    public void Place(IReadOnlyList<ICopy> placed, IAreaView area)
    {
        foreach (var copy in placed.Where(c => Kind(c) == RailKind.Track || c.Prefab == RailroadsMod.OldStation))
        {
            copy.SetQuaternion(Keys.Turn, copy.Orientation);
        }
        if (!placed.Any(c => Kind(c) == RailKind.Vehicle || c.Prefab == RailroadsMod.Sign))
        {
            return;
        }
        var tracks = RailroadsMod.Pieces.Where(p => p.Kind == RailKind.Track).SelectMany(p => area.ObjectsOf(p.Prefab))
            .Concat(placed.Where(c => Kind(c) == RailKind.Track));
        var network = TrackNetwork.Of(tracks, models);
        foreach (var copy in placed)
        {
            if (Kind(copy) == RailKind.Vehicle)
            {
                PutOnTrack(copy, network);
            }
            else if (copy.Prefab == RailroadsMod.Sign)
            {
                var stop = network.Nearest(copy.Position, SignReach, network.StopsAt);
                copy.SetVector3(Keys.Stop, stop?.Point);
            }
        }
    }

    /// <summary>A vehicle on the track nearest it (or on none), stopped.</summary>
    private static void PutOnTrack(ICopy copy, TrackNetwork network)
    {
        foreach (string key in Trip)
        {
            copy.SetBytes(key, null);
            copy.SetString(key, null);
        }
        copy.SetInt(Keys.Moving, null);
        copy.SetInt(Keys.Way, null);
        copy.SetFloat(Keys.Stop, null);
        copy.SetFloat(Keys.Speed, null);
        copy.SetString(Keys.Destination, null);
        copy.SetLong(Keys.SentBy, null);
        copy.SetString(Keys.Line, null);
        if (network.Nearest(copy.Position, VehicleReach) is not var (line, at, _))
        {
            copy.SetBytes(Keys.Line, null);
            copy.SetFloat(Keys.At, null);
            copy.SetInt(Keys.Dir, null);
            return;
        }
        copy.SetBytes(Keys.Line, TrackLines.Write([(line.A, line.B)]));
        copy.SetFloat(Keys.At, at);
        // The mod keeps the way only when it is backward (1 is its default).
        bool forward = Vector3.Dot(line.ForwardAt(at), Vector3.Transform(Vector3.UnitZ, copy.Orientation)) >= 0;
        copy.SetInt(Keys.Dir, forward ? null : -1);
    }

    /// <summary>What a placed object is, null for what is not one of the mod's pieces.</summary>
    private static RailKind? Kind(IObjectView obj) => RailroadsMod.Pieces.FirstOrDefault(p => p.Prefab == obj.Prefab)?.Kind;
}
