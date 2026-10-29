using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// One of the mod's pieces drawn as the mod builds it while the game runs, from its own models (<see cref="ModelLibrary"/>):
/// its body, and for a vehicle its wheels under each axle (the locomotive's big driving wheels too), its couplers
/// standing straight out and the hand cart's pump handle as it leans, for the old station its legs unless it was built
/// without, for the drawbridge its leaves, chains and weights raised or lowered (<see cref="DrawbridgeParts"/>). Each
/// part in its material's colour. The levers, rods, lamps and loads the mod adds to its vehicles are left out.
/// </summary>
/// <param name="piece">The piece.</param>
/// <param name="models">The mod's models.</param>
public sealed class PieceShapes(RailPiece piece, ModelLibrary models) : IShapeProvider
{
    /// <summary>Wood, as the mod's wood beams look.</summary>
    public static readonly Vector4 Wood = new(0.36f, 0.25f, 0.15f, 1);

    /// <summary>Stone, as the mod's stone walls look.</summary>
    public static readonly Vector4 Stone = new(0.45f, 0.44f, 0.41f, 1);

    /// <summary>Iron, as the mod's iron beams look.</summary>
    public static readonly Vector4 Iron = new(0.17f, 0.17f, 0.19f, 1);

    private readonly object _gate = new();

    /// <summary>The shapes that do not change with the object's data, and the models they were made from.</summary>
    private (RailModel?[] From, Shape[] Shapes)? _fixed;

    /// <summary>The drawbridge's moving parts, lowered then raised, and the models they were made from.</summary>
    private readonly (RailModel?[] From, Shape[] Shapes)?[] _bridge = new (RailModel?[], Shape[])?[2];

    /// <summary>The colour a material slot is drawn in (a slot the mod does not have: iron's).</summary>
    public static Vector4 ColourOf(string slot) => slot switch
    {
        "wood" => Wood,
        "stone" => Stone,
        _ => Iron,
    };

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        var shapes = new List<Shape>(Fixed());
        if (piece.Prefab == RailroadsMod.OldStation && !(obj.TryGetInt(RailroadsMod.Keys.Bare, out int bare) && bare != 0)
            && models.Get(RailroadsMod.StationLegs) is { } legs)
        {
            shapes.AddRange(Placed(legs, Vector3.Zero, Quaternion.Identity));
        }
        if (piece.Prefab == RailroadsMod.Handcart && models.Get(RailroadsMod.Beam) is { } beam)
        {
            float lean = obj.TryGetFloat(RailroadsMod.Keys.Beam, out float b) && !float.IsNaN(b) && !float.IsInfinity(b) ? b : 0;
            shapes.AddRange(Placed(beam, RailroadsMod.BeamAt, Quaternion.CreateFromAxisAngle(Vector3.UnitX, lean * RailroadsMod.BeamDegrees * (float)Math.PI / 180)));
        }
        if (piece.Prefab == RailroadsMod.Drawbridge)
        {
            shapes.AddRange(Bridge(DrawbridgeParts.IsUp(obj)));
        }
        return shapes;
    }

    /// <summary>The body and its wheels, made again only when one of their models was read again.</summary>
    private Shape[] Fixed()
    {
        var body = models.Get(piece.Model);
        RailModel? wheels = null, big = null, coupler = null;
        if (piece.Kind == RailKind.Vehicle && body?.Axles.Count > 0)
        {
            wheels = models.Get(piece.Prefab == RailroadsMod.Locomotive ? RailroadsMod.SmallAxle : RailroadsMod.Axle);
        }
        if (piece.Prefab == RailroadsMod.Locomotive)
        {
            big = models.Get(RailroadsMod.BigAxle);
        }
        // The models of 0.3.0 on have no coupler but where to build it; before, the body had it.
        if (piece.Kind == RailKind.Vehicle && body?.Boxes.Any(IsCoupler) == true)
        {
            coupler = models.Get(RailroadsMod.Coupler);
        }
        RailModel?[] from = [body, wheels, big, coupler];
        lock (_gate)
        {
            if (_fixed is { } made && made.From.SequenceEqual(from))
            {
                return made.Shapes;
            }
            var shapes = new List<Shape>();
            if (body != null)
            {
                shapes.AddRange(Placed(body, Vector3.Zero, Quaternion.Identity));
                if (wheels != null)
                {
                    foreach (var axle in body.Axles)
                    {
                        shapes.AddRange(Placed(wheels, axle, Quaternion.Identity));
                    }
                }
                if (coupler != null)
                {
                    foreach (var box in body.Boxes.Where(IsCoupler))
                    {
                        // From the box's inner end outwards: forward at the front, turned half round at the back.
                        float end = Math.Sign(box.Center.Z);
                        var at = box.Center - (Vector3.UnitZ * end * RailroadsMod.CouplerBox.Z / 2);
                        shapes.AddRange(Placed(coupler, at, end > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI)));
                    }
                }
            }
            if (big != null)
            {
                shapes.AddRange(Placed(big, RailroadsMod.BigAxleAt, Quaternion.Identity));
            }
            _fixed = (from, shapes.ToArray());
            return _fixed.Value.Shapes;
        }
    }

    /// <summary>A box of a vehicle's model where the mod builds a coupler: one of the coupler's size.</summary>
    private static bool IsCoupler(RailModel.Box box) => (box.Size - RailroadsMod.CouplerBox).LengthSquared() < 1e-4f;

    /// <summary>
    /// The drawbridge's moving parts, made again only when one of their models was read again: one shape a material,
    /// for the hundreds of links of its chains.
    /// </summary>
    private Shape[] Bridge(bool up)
    {
        RailModel?[] from = [.. DrawbridgeParts.Models.Select(models.Get)];
        int i = up ? 1 : 0;
        lock (_gate)
        {
            if (_bridge[i] is { } made && made.From.SequenceEqual(from))
            {
                return made.Shapes;
            }
            var byName = DrawbridgeParts.Models.Zip(from, (name, model) => (name, model)).ToDictionary(m => m.name, m => m.model);
            var parts = DrawbridgeParts.Parts(up ? 1 : 0)
                .SelectMany(p => byName[p.Model] is { } model ? Placed(model, p.At, p.Turn) : []);
            _bridge[i] = (from, Merged(parts));
            return _bridge[i]!.Value.Shapes;
        }
    }

    /// <summary>Shapes of one colour made one.</summary>
    private static Shape[] Merged(IEnumerable<Shape> shapes) =>
    [
        .. shapes.GroupBy(s => s.Color).Select(same =>
        {
            var corners = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var shape in same)
            {
                int first = corners.Count;
                corners.AddRange(shape.Vertices);
                triangles.AddRange(shape.Triangles.Select(t => t + first));
            }
            return new Shape([.. corners], [.. triangles], same.Key);
        }),
    ];

    /// <summary>A model's parts as shapes, turned and then placed in the piece's frame.</summary>
    /// <param name="model">The model.</param>
    /// <param name="at">Where its origin is in the piece's frame.</param>
    /// <param name="turn">How it is turned about its origin.</param>
    public static IEnumerable<Shape> Placed(RailModel model, Vector3 at, Quaternion turn)
    {
        foreach (var part in model.Parts)
        {
            var corners = new Vector3[part.Positions.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = at + Vector3.Transform(part.Positions[i], turn);
            }
            yield return new Shape(corners, part.Triangles, ColourOf(part.Slot));
        }
    }
}
