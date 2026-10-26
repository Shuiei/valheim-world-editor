using System.Numerics;

namespace Railroads.WorldEditor;

/// <summary>
/// Railroads, the mod of stone track, trains and stations (by VladasZ), as the editor knows it: its plugin file, its
/// pieces and the models it builds them from, and the data keys it keeps on them.
/// </summary>
/// <remarks>Followed: Railroads 0.2.9.</remarks>
public static class RailroadsMod
{
    /// <summary>The mod's plugin file, under a BepInEx plugins folder; its models are in <see cref="MeshFolder"/> beside it.</summary>
    public const string DllName = "Railroads.dll";

    /// <summary>The folder beside the plugin holding the mod's model files (<see cref="ModelFile"/>).</summary>
    public const string MeshFolder = "meshes";

    /// <summary>The prefab of the locomotive.</summary>
    public const string Locomotive = "bf_locomotive";

    /// <summary>The prefab of the hand cart.</summary>
    public const string Handcart = "bf_handcart";

    /// <summary>The prefab of a station sign, a named stop for trains.</summary>
    public const string Sign = "bf_station_sign";

    /// <summary>The prefab of the station of the mod's first versions (no longer on the hammer; still in worlds).</summary>
    public const string OldStation = "bf_station";

    /// <summary>
    /// The prefab of the object the server keeps the count of numbered trains on (<see cref="Keys.TrainCount"/>): one
    /// in a world, at its centre, with nothing to see. No piece: the mod makes it when it first numbers a train.
    /// </summary>
    public const string Register = "bf_rail_world";

    /// <summary>
    /// Every piece the mod adds, with the model it is built from (meshes/&lt;model&gt;.bfm beside the plugin) and what it
    /// is. Retired pieces are no longer on the hammer, but stand in worlds built with older versions.
    /// </summary>
    public static readonly IReadOnlyList<RailPiece> Pieces =
    [
        Track("bf_track_straight", "track_straight"),
        Track("bf_track_straight_1", "track_straight_1"),
        Track("bf_track_straight_4", "track_straight_4"),
        Track("bf_track_straight_8", "track_straight_8"),
        Track("bf_track_straight_16", "track_straight_16"),
        Track("bf_track_straight_32", "track_straight_32"),
        Track("bf_track_curve_r8_22", "track_curve_r8_22"),
        Track("bf_track_curve_r8_45", "track_curve_r8_45"),
        Track("bf_track_curve_r8_90", "track_curve_r8_90"),
        Track("bf_track_curve_r16_22", "track_curve_r16_22"),
        Track("bf_track_curve_r16_45", "track_curve_r16_45"),
        Track("bf_track_curve_r32_22", "track_curve_r32_22"),
        Track("bf_track_slope_gentle", "track_slope_gentle"),
        Track("bf_track_slope_steep", "track_slope_steep"),
        Track("bf_track_slope_bottom_gentle", "track_slope_bottom_gentle"),
        Track("bf_track_slope_top_gentle", "track_slope_top_gentle"),
        Track("bf_track_slope_bottom_steep", "track_slope_bottom_steep"),
        Track("bf_track_slope_top_steep", "track_slope_top_steep"),
        Track("bf_track_switch_right", "track_switch_right", retired: true),
        Track("bf_track_switch_left", "track_switch_left", retired: true),
        Track("bf_track_switch_right_8", "track_switch_right_8"),
        Track("bf_track_switch_left_8", "track_switch_left_8"),
        Track("bf_track_crossing", "track_crossing"),
        Track("bf_track_loop", "track_loop"),
        Track("bf_track_junction_t", "track_junction_t"),
        Track("bf_track_junction_x", "track_junction_x"),
        new(OldStation, "station", RailKind.Station, Retired: true),
        new(Sign, "station_sign", RailKind.Station),
        new("bf_train_console", "train_console", RailKind.Station),
        new(Locomotive, "locomotive", RailKind.Vehicle),
        new("bf_wagon", "wagon", RailKind.Vehicle),
        new("bf_wagon_restaurant", "restaurant", RailKind.Vehicle),
        new("bf_wagon_portal", "portal", RailKind.Vehicle),
        new("bf_wagon_flat", "flatbed", RailKind.Vehicle),
        new(Handcart, "handcart", RailKind.Vehicle),
    ];

    /// <summary>The old station's legs (unless it keeps <see cref="Keys.Bare"/>), drawn from their own model.</summary>
    public const string StationLegs = "station_legs";

    /// <summary>The wheel set under each of a wagon's or the hand cart's axles (<see cref="RailModel.Axles"/>).</summary>
    public const string Axle = "axle";

    /// <summary>The wheel set under each of the locomotive's own axles.</summary>
    public const string SmallAxle = "axle_small";

    /// <summary>The locomotive's big driving wheels, at <see cref="BigAxleAt"/>.</summary>
    public const string BigAxle = "axle_big";

    /// <summary>Where the locomotive's big driving wheels turn, in its frame.</summary>
    public static readonly Vector3 BigAxleAt = new(0, 1.19f, -1.8f);

    /// <summary>The hand cart's pump handle, turning about <see cref="BeamAt"/>.</summary>
    public const string Beam = "handcart_beam";

    /// <summary>Where the hand cart's pump handle turns, in its frame.</summary>
    public static readonly Vector3 BeamAt = new(0, 2.27f, 0);

    /// <summary>How far the pump handle leans per unit of <see cref="Keys.Beam"/>, in degrees about its axis.</summary>
    public const float BeamDegrees = 12;

    /// <summary>A piece of track: its prefab, its model, and whether it is retired.</summary>
    private static RailPiece Track(string prefab, string model, bool retired = false) => new(prefab, model, RailKind.Track, retired);

    /// <summary>The data keys the mod keeps, as its code names them.</summary>
    public static class Keys
    {
        /// <summary>A track piece's or old station's exact turn (quaternion), which the mod gives it back as the world loads.</summary>
        public const string Turn = "bf_turn";

        /// <summary>A station's name (string).</summary>
        public const string Name = "bf_name";

        /// <summary>A station sign's stop on the track (vec3, world); a locomotive's stop along its route (float).</summary>
        public const string Stop = "bf_stop";

        /// <summary>An old station built without its legs (bool).</summary>
        public const string Bare = "bf_bare";

        /// <summary>The track line a vehicle stands on (bytes, <see cref="TrackLines"/>).</summary>
        public const string Line = "bf_line";

        /// <summary>How far along its line a vehicle stands, in metres (float).</summary>
        public const string At = "bf_at";

        /// <summary>Which way along its line a vehicle faces (int: 1 or -1).</summary>
        public const string Dir = "bf_dir";

        /// <summary>The lines a train's wagons stand on behind it (bytes, <see cref="TrackLines"/>).</summary>
        public const string Trail = "bf_trail";

        /// <summary>The lines a sent train still runs on to its stop (bytes, <see cref="TrackLines"/>).</summary>
        public const string Route = "bf_route";

        /// <summary>The lines past the stop its wagons need (bytes, <see cref="TrackLines"/>).</summary>
        public const string After = "bf_after";

        /// <summary>The legs of a trip after the one under way (bytes, <see cref="TripLegs"/>).</summary>
        public const string Legs = "bf_legs";

        /// <summary>A train sent somewhere and on its way (bool).</summary>
        public const string Moving = "bf_moving";

        /// <summary>The wagons a train pulls, in order (bytes, <see cref="ObjectIds"/>).</summary>
        public const string Wagons = "bf_wagons";

        /// <summary>A wagon's own object id as last seen (bytes, <see cref="ObjectIds"/>): how the mod finds its wagons again.</summary>
        public const string Self = "bf_self";

        /// <summary>The coal in a locomotive (int).</summary>
        public const string Coal = "bf_coal";

        /// <summary>The metres run on the coal last burnt (float).</summary>
        public const string Odometer = "bf_odo";

        /// <summary>A train's speed, metres a second (float).</summary>
        public const string Speed = "bf_speed";

        /// <summary>The player driving (long).</summary>
        public const string Driver = "bf_driver";

        /// <summary>The station a train is sent to (string).</summary>
        public const string Destination = "bf_dest";

        /// <summary>The switch lever (int).</summary>
        public const string Switch = "bf_switch";

        /// <summary>The speed or way lever (int).</summary>
        public const string Gear = "bf_gear";

        /// <summary>Which way a sent train runs its leg (int: 1 or -1).</summary>
        public const string Way = "bf_way";

        /// <summary>The trips a train was sent on (int).</summary>
        public const string Trips = "bf_trips";

        /// <summary>The whistle sound a sent train blows (string).</summary>
        public const string Horn = "bf_horn";

        /// <summary>The hand cart's pump handle (float, leaning <see cref="BeamDegrees"/> per unit).</summary>
        public const string Beam = "bf_beam";

        /// <summary>The player holding the hand cart's front handle (long).</summary>
        public const string Front = "bf_front";

        /// <summary>The player holding the hand cart's back handle (long).</summary>
        public const string Back = "bf_back";

        /// <summary>A flatbed's own deck id (long), which the pieces built on it name.</summary>
        public const string DeckKey = "bf_deck_key";

        /// <summary>The flatbed a piece is built on (long, its <see cref="DeckKey"/>).</summary>
        public const string Deck = "bf_deck";

        /// <summary>A piece's place on its flatbed's deck, in the flatbed's frame (vec3).</summary>
        public const string DeckPos = "bf_deck_pos";

        /// <summary>A piece's turn on its flatbed's deck, in the flatbed's frame (quaternion).</summary>
        public const string DeckRot = "bf_deck_rot";

        /// <summary>The fuel left in a vehicle's first lamp (float; the mod names its lamps' keys by number).</summary>
        public const string Torch0 = "bf_torch0";

        /// <summary>The fuel left in a vehicle's second lamp (float).</summary>
        public const string Torch1 = "bf_torch1";

        /// <summary>When the first lamp's fuel was last counted (long, the game's ticks).</summary>
        public const string Torch0Time = "bf_torch0_time";

        /// <summary>When the second lamp's fuel was last counted (long, the game's ticks).</summary>
        public const string Torch1Time = "bf_torch1_time";

        /// <summary>A wagon's sign (string).</summary>
        public const string Sign = "bf_sign";

        /// <summary>The restaurant wagon's name (string).</summary>
        public const string Cafe = "bf_cafe";

        /// <summary>A train's name, on its locomotive (string; empty or missing: the train is called by its <see cref="TrainNumber"/>).</summary>
        public const string TrainName = "bf_train_name";

        /// <summary>A train's number, on its locomotive (int), which the server gives each train it has not numbered yet.</summary>
        public const string TrainNumber = "bf_train_number";

        /// <summary>The last number the server gave a train (int), on the <see cref="Register"/>.</summary>
        public const string TrainCount = "bf_train_count";

        /// <summary>What a support pole under track is made of (int).</summary>
        public const string Leg = "bf_leg";

        /// <summary>Which part of a support a pole is (int).</summary>
        public const string LegPart = "bf_leg_part";

        /// <summary>A support built for free (bool).</summary>
        public const string LegFree = "bf_leg_free";

        /// <summary>A track piece's price is known (bool).</summary>
        public const string Paid = "bf_paid";

        /// <summary>The wood a track piece was paid with (int).</summary>
        public const string PaidWood = "bf_paid_wood";

        /// <summary>The stone a track piece was paid with (int).</summary>
        public const string PaidStone = "bf_paid_stone";

        /// <summary>The core wood a track piece was paid with (int).</summary>
        public const string PaidCoreWood = "bf_corewood";
    }
}

/// <summary>What a piece of the mod is.</summary>
public enum RailKind
{
    /// <summary>Track: rails a train runs on.</summary>
    Track,

    /// <summary>A station, its sign or a train console.</summary>
    Station,

    /// <summary>A locomotive, a wagon or the hand cart: it runs on the track.</summary>
    Vehicle,
}

/// <summary>One piece the mod adds.</summary>
/// <param name="Prefab">Its prefab.</param>
/// <param name="Model">The model it is built from (meshes/&lt;model&gt;.bfm beside the plugin).</param>
/// <param name="Kind">What it is.</param>
/// <param name="Retired">No longer on the hammer (still in worlds built with older versions).</param>
public sealed record RailPiece(string Prefab, string Model, RailKind Kind, bool Retired = false);
