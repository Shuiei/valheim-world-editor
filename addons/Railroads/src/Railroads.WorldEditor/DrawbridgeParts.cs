using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// The drawbridge's moving parts (<see cref="RailroadsMod.Drawbridge"/>, Railroads 0.3.1), placed as the mod lays them
/// out for how far its leaves are raised: a leaf on its hinge at each end of its 16 m of track, and at each end, on each
/// side, a chain from the leaf up over a sheave in the tower and down to a counterweight, with a pinion turning beside
/// the sheave. The chain is always as long, so the weight hangs lower as the leaf rises. The levers are left out. The
/// second end is the first turned half round about the bridge's middle.
/// </summary>
public static class DrawbridgeParts
{
    /// <summary>A leaf: 8 m of track on its hinge.</summary>
    public const string Leaf = "track_drawbridge_leaf";

    /// <summary>A sheave the chain runs over, in a tower.</summary>
    public const string Sheave = "track_drawbridge_sheave";

    /// <summary>The pinion beside each sheave.</summary>
    public const string Pinion = "track_drawbridge_pinion";

    /// <summary>A counterweight at a chain's end.</summary>
    public const string Weight = "track_drawbridge_weight";

    /// <summary>A link of a chain.</summary>
    public const string Link = "track_drawbridge_link";

    /// <summary>The models of the moving parts.</summary>
    public static readonly string[] Models = [Leaf, Sheave, Pinion, Weight, Link];

    /// <summary>The bridge's length: the second leaf's hinge is that far along it.</summary>
    public const float Length = 16;

    /// <summary>The first leaf's hinge, along the bridge (x here) and up (y).</summary>
    private static readonly Vector2 Hinge = new(0, 0.34f);

    /// <summary>Where the chain holds the leaf, in the leaf's frame (along it, up).</summary>
    private static readonly Vector2 Eye = new(4, -0.09f);

    /// <summary>The sheave's axle, along the bridge and up: before the first leaf's hinge, high in the tower.</summary>
    private static readonly Vector2 Axle = new(-1.2f, 10.05f);

    /// <summary>The sheave's radius: the chain runs over it.</summary>
    private const float Radius = 0.5f;

    /// <summary>How far below the sheave's axle the chain hangs while the leaf is down: there the weight hangs from it.</summary>
    private const float HangsDown = 0.5f;

    /// <summary>How far to each side of the track the chains, sheaves and weights are.</summary>
    private const float Side = 1.42f;

    /// <summary>The pinion beside the sheave on the right: to the side, up, along.</summary>
    private static readonly Vector3 PinionAt = new(1.3f, 9.65402f, -0.8040203f);

    /// <summary>How many times the sheave's turn the pinion turns, the other way.</summary>
    private const float PinionRatio = 3;

    /// <summary>The pinion's turn while the leaf is down.</summary>
    private const float PinionStart = (float)Math.PI / 8;

    /// <summary>How long a link of the chain is.</summary>
    private const float LinkLength = 0.22f;

    /// <summary>A half turn about the vertical: the second end's frame, and a part on the left facing the other way.</summary>
    private static readonly Quaternion HalfRound = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);

    /// <summary>
    /// Whether a drawbridge's leaves are up. It is drawn as it ends its swing: the swing takes 8 s from when it began
    /// (<see cref="RailroadsMod.Keys.BridgeSince"/>, by the game's clock, which the editor does not have).
    /// </summary>
    public static bool IsUp(IObjectView bridge) => bridge.TryGetInt(RailroadsMod.Keys.BridgeUp, out int up) && up != 0;

    /// <summary>Each moving part: its model, where its origin is and how it is turned, in the bridge's frame.</summary>
    /// <param name="raised">How far the leaves are raised, 0 down to 1 up.</param>
    public static IEnumerable<(string Model, Vector3 At, Quaternion Turn)> Parts(float raised)
    {
        var chain = new Chain(Math.Min(1, Math.Max(0, raised)) * (float)Math.PI / 2);
        var down = new Chain(0);
        // The chain is as long whatever the leaf does: what it no longer needs between the leaf and the sheave hangs
        // past the sheave, the sheave turning with it.
        float length = down.ToSheave + HangsDown;
        float hangs = length - chain.ToSheave;
        float spin = -(hangs - HangsDown) / Radius;
        var parts = new List<(string, Vector3, Quaternion)>
        {
            (Leaf, new Vector3(0, Hinge.Y, Hinge.X), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -chain.Angle)),
        };
        foreach (int side in (int[])[-1, 1])
        {
            parts.Add((Sheave, new Vector3(side * Side, Axle.Y, Axle.X), Wheel(side, spin)));
            parts.Add((Pinion, new Vector3(side * PinionAt.X, PinionAt.Y, PinionAt.Z), Wheel(side, (-PinionRatio * spin) + PinionStart)));
            parts.Add((Weight, new Vector3(side * Side, Axle.Y - hangs, Axle.X - Radius), Quaternion.Identity));
            for (int k = 0; LinkLength * (k + 1) <= length; k++)
            {
                var (at, way) = chain.At(LinkLength * (k + 0.5f));
                var forward = new Vector3(0, way.Y, way.X);
                var turn = Look(forward, Math.Abs(forward.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY);
                // Every other link stands across the one before it, as in a chain.
                parts.Add((Link, new Vector3(side * Side, at.Y, at.X), k % 2 == 0 ? turn : turn * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2)));
            }
        }
        // The second end: the first turned half round about the bridge's middle.
        foreach (var (model, at, turn) in parts.ToList())
        {
            parts.Add((model, new Vector3(-at.X, at.Y, Length - at.Z), HalfRound * turn));
        }
        return parts;
    }

    /// <summary>A sheave's or pinion's turn about its axle, on the right; on the left it faces the other way.</summary>
    private static Quaternion Wheel(int side, float spin) => side > 0
        ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, spin)
        : HalfRound * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -spin);

    /// <summary>The turn that faces forward with up as near up as it goes (Unity's look rotation).</summary>
    private static Quaternion Look(Vector3 forward, Vector3 up)
    {
        var z = Vector3.Normalize(forward);
        var x = Vector3.Normalize(Vector3.Cross(up, z));
        var y = Vector3.Cross(z, x);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1)));
    }

    /// <summary>
    /// The chain at the first end on a side, the leaf raised by an angle, seen from the side (along the bridge, up): from
    /// its eye on the leaf straight to where it meets the sheave, over the sheave, then straight down. A record: equal when
    /// its values are.
    /// </summary>
    private readonly record struct Chain
    {
        /// <summary>The chain with its leaf raised by an angle.</summary>
        /// <param name="angle">The leaf's angle up from flat, in radians.</param>
        public Chain(float angle)
        {
            Angle = angle;
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            EyeAt = Hinge + new Vector2((Eye.X * cos) - (Eye.Y * sin), (Eye.X * sin) + (Eye.Y * cos));
            var toEye = EyeAt - Axle;
            float far = toEye.Length();
            Straight = (float)Math.Sqrt((far * far) - (Radius * Radius));
            Meets = (float)(Math.Atan2(toEye.Y, toEye.X) + Math.Acos(Radius / far));
            Over = Radius * ((float)Math.PI - Meets);
        }

        /// <summary>The leaf's angle up from flat, in radians.</summary>
        public float Angle { get; }

        /// <summary>The chain's eye on the leaf.</summary>
        public Vector2 EyeAt { get; }

        /// <summary>How long the chain is from the eye to where it meets the sheave.</summary>
        public float Straight { get; }

        /// <summary>Where it meets the sheave, as an angle about the axle (0 along the bridge, π/2 up).</summary>
        public float Meets { get; }

        /// <summary>How long it lies over the sheave, to where it goes straight down.</summary>
        public float Over { get; }

        /// <summary>How long it is from the eye to where it goes straight down past the sheave.</summary>
        public float ToSheave => Straight + Over;

        /// <summary>The point of the chain a length from its eye, and its way there (along the bridge, up).</summary>
        /// <param name="along">The length from the eye.</param>
        public (Vector2 At, Vector2 Way) At(float along)
        {
            var meets = Axle + (Radius * new Vector2((float)Math.Cos(Meets), (float)Math.Sin(Meets)));
            if (along <= Straight)
            {
                var way = (meets - EyeAt) / Straight;
                return (EyeAt + (way * along), way);
            }
            if (along <= ToSheave)
            {
                float a = Meets + ((along - Straight) / Radius);
                return (Axle + (Radius * new Vector2((float)Math.Cos(a), (float)Math.Sin(a))), new Vector2(-(float)Math.Sin(a), (float)Math.Cos(a)));
            }
            return (new Vector2(Axle.X - Radius, Axle.Y - (along - ToSheave)), -Vector2.UnitY);
        }
    }
}
