using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor;

/// <summary>
/// One of the mod's pieces drawn as the mod builds it while the game runs, from its own models (<see cref="ModelLibrary"/>):
/// its body, and for a vehicle its wheels under each axle (the locomotive's big driving wheels too) and the hand cart's
/// pump handle as it leans, for the old station its legs unless it was built without. Each part in its material's
/// colour. The levers, rods, lamps and loads the mod adds to its vehicles are left out.
/// </summary>
/// <param name="piece">The piece.</param>
/// <param name="models">The mod's models.</param>
public sealed class PieceShapes(RailPiece piece, ModelLibrary models) : IShapeProvider
{
    /// <summary>Wood, as the mod's wood beams look.</summary>
    public static readonly Vector4 Wood = new(0.52f, 0.38f, 0.25f, 1);

    /// <summary>Stone, as the mod's stone walls look.</summary>
    public static readonly Vector4 Stone = new(0.6f, 0.59f, 0.56f, 1);

    /// <summary>Iron, as the mod's iron beams look.</summary>
    public static readonly Vector4 Iron = new(0.3f, 0.3f, 0.32f, 1);

    private readonly object _gate = new();

    /// <summary>The shapes that do not change with the object's data, and the models they were made from.</summary>
    private (RailModel?[] From, Shape[] Shapes)? _fixed;

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
        return shapes;
    }

    /// <summary>The body and its wheels, made again only when one of their models was read again.</summary>
    private Shape[] Fixed()
    {
        var body = models.Get(piece.Model);
        RailModel? wheels = null, big = null;
        if (piece.Kind == RailKind.Vehicle && body?.Axles.Count > 0)
        {
            wheels = models.Get(piece.Prefab == RailroadsMod.Locomotive ? RailroadsMod.SmallAxle : RailroadsMod.Axle);
        }
        if (piece.Prefab == RailroadsMod.Locomotive)
        {
            big = models.Get(RailroadsMod.BigAxle);
        }
        RailModel?[] from = [body, wheels, big];
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
            }
            if (big != null)
            {
                shapes.AddRange(Placed(big, RailroadsMod.BigAxleAt, Quaternion.Identity));
            }
            _fixed = (from, shapes.ToArray());
            return _fixed.Value.Shapes;
        }
    }

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
