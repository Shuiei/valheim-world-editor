using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the Area tool's ground actions and for nature: smoothing, naturalizing, eroding, restoring,
/// raising and lowering a rectangle of ground, and regrowing what the game grows there.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- The Area tool's ground actions, and nature.

    /// <summary>The ground actions shape_ground takes, by name.</summary>
    internal static readonly Dictionary<string, AreaTool.GroundAction> GroundActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smooth"] = AreaTool.GroundAction.Smooth,
        ["naturalize"] = AreaTool.GroundAction.Natural,
        ["erode"] = AreaTool.GroundAction.Erode,
        ["restore"] = AreaTool.GroundAction.Restore,
        ["raise"] = AreaTool.GroundAction.Raise,
        ["lower"] = AreaTool.GroundAction.Lower,
    };

    /// <summary>
    /// The Area tool's box over a world rectangle of the scene (its corners in grid points), with a soft edge of
    /// edge metres going inwards.
    /// </summary>
    internal static AreaTool BoxOver(WorldScene scene, float x0, float z0, float x1, float z1, float edge)
    {
        float ox = scene.X0 * 64f - 32f, oz = scene.Z0 * 64f - 32f;
        var box = new AreaTool { Box = true, Soft = MathF.Max(0, edge) };
        box.Points.Add(new Vector2(MathF.Min(x0, x1) - ox, MathF.Min(z0, z1) - oz));
        box.Points.Add(new Vector2(MathF.Max(x0, x1) - ox, MathF.Max(z0, z1) - oz));
        return box;
    }

    /// <summary>shape_ground: an Area tool ground action over a rectangle, as one pending step.</summary>
    [McpServerTool(Name = "shape_ground", Title = "Smooth, naturalize, erode or restore ground")]
    [Description("Changes the ground of a rectangle of the open area as the Area tool does, fading in over a soft edge going inwards: smooth (evens out bumps and steps), naturalize (natural-looking bumps, bumps metres high and bumpSize metres wide: what made-up ground needs to look real), erode (weathers it: slopes settle to restAngle degrees and rain cuts gullies), restore (back to the ground the game generated, paint taken away), raise or lower (by amount metres). One pending step. The game's ±8 m limit from the original ground holds.")]
    public Task<string> ShapeGround(
        [Description("smooth, naturalize, erode, restore, raise or lower.")] string action,
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("The soft edge, metres inwards from the rectangle's sides (default 3; 0: hard).")] float edge = 3,
        [Description("raise and lower: metres (default 1).")] float amount = 1,
        [Description("naturalize: how high the bumps are, metres (default 1.2).")] float bumps = 1.2f,
        [Description("naturalize: how wide the bumps are, metres (default 16).")] float bumpSize = 16,
        [Description("erode: the slope ground settles to, degrees (default 33).")] float restAngle = 33,
        [Description("naturalize and erode: any number, another pattern with the same choices.")] int seed = 1) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (!GroundActions.TryGetValue(action, out var act))
        {
            return "The action is smooth, naturalize, erode, restore, raise or lower.";
        }
        var s = _w.Session!;
        var (minX, minZ, maxX, maxZ) = Bounds(s.Scene);
        if (MathF.Max(x0, x1) < minX || MathF.Min(x0, x1) > maxX || MathF.Max(z0, z1) < minZ || MathF.Min(z0, z1) > maxZ)
        {
            return $"The rectangle is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        var box = BoxOver(s.Scene, x0, z0, x1, z1, edge);
        var brush = new Brush
        {
            NoiseAmp = Math.Clamp(bumps, 0, 20),
            NoiseSize = Math.Clamp(bumpSize, 2, 200),
            Noise = new Noise(seed),
            RestAngle = Math.Clamp(restAngle, 5, 80),
            Random = new Random(seed),
        };
        string name = AreaTool.Label(act).ToLowerInvariant();
        var touched = s.EditGround($"Claude: {name}", g => box.Apply(g, brush, act, 0, MathF.Max(0, amount), [0, 0, 0, 1]));
        _w.AfterClaudeEdit($"Claude's {name}: {touched.Count} ground point(s); Ctrl+Z takes it back.");
        return touched.Count == 0 ? "Nothing changed." : $"{AreaTool.Label(act)}: {touched.Count} ground point(s) changed. Now pending: {Pending()}";
    });

    /// <summary>regrow_nature: what the game grows in a rectangle (Growth), of the kinds asked, where nothing stands.</summary>
    [McpServerTool(Name = "regrow_nature", Title = "Regrow nature")]
    [Description("Regrows nature over a rectangle of the open area of a world, as the Area tool's Regrow nature does: the trees, rocks, bushes and pickables the game itself grows there from the world's seed (its kinds, sizes and places, for the biome), left out where something stands (anything within 1 m, the same kind within 3 m, buildings within 4 m). Use it after clearing or reshaping ground to make it look untouched. Up to 16 zones (256 x 256 m) at once. One pending step.")]
    public async Task<string> RegrowNature(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Kinds to grow: Trees, Rocks, Bushes, Pickables (default: all four).")] string[]? kinds = null)
    {
        var chosen = new HashSet<TerrainEditor.App.ObjectKind>();
        foreach (string k in kinds is { Length: > 0 } ? kinds : ["Trees", "Rocks", "Bushes", "Pickables"])
        {
            if (!Enum.TryParse<TerrainEditor.App.ObjectKind>(k, true, out var kind) || kind is not (TerrainEditor.App.ObjectKind.Trees or TerrainEditor.App.ObjectKind.Rocks or TerrainEditor.App.ObjectKind.Bushes or TerrainEditor.App.ObjectKind.Pickables))
            {
                return $"{k}: the kinds are Trees, Rocks, Bushes and Pickables.";
            }
            chosen.Add(kind);
        }
        string? problem = null;
        var session = await OnUi(() =>
        {
            if (CannotEdit() is string why)
            {
                problem = why;
                return null;
            }
            if (_w.InWorkshop)
            {
                problem = "Nature regrows in a world, not on the Workshop's plot.";
                return null;
            }
            return _w.Session;
        });
        if (session == null)
        {
            return problem!;
        }
        float ax = MathF.Min(x0, x1), az = MathF.Min(z0, z1), bx = MathF.Max(x0, x1), bz = MathF.Max(z0, z1);
        string said = "";
        var spots = await OnUi(() => Growth.Spots(session, ax, az, bx, bz,
            o => o.X >= ax && o.X <= bx && o.Z >= az && o.Z <= bz && chosen.Contains(TerrainEditor.App.ObjectKinds.Of(o.Name, false)),
            _w.NameOfPrefab, m => said = m));
        if (spots == null)
        {
            return said;
        }
        if (spots.Count == 0)
        {
            return "Nothing to regrow: the game grows none of those kinds here, or it all still stands.";
        }
        return await OnUi(() =>
        {
            // The area may have changed while the spots were worked out.
            if (_w.Session != session || CannotEdit() is string why)
            {
                return "The open area changed meanwhile: nothing regrown.";
            }
            Growth.Commit(session, $"Claude: regrew {spots.Count} object(s)", spots);
            _w.AfterClaudeEdit($"Claude regrew {spots.Count} object(s); Ctrl+Z takes them back.");
            var counts = spots.GroupBy(o => o.Name).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}");
            return $"Regrew {spots.Count} object(s): {string.Join(", ", counts.Take(12))}. Now pending: {Pending()}";
        });
    }
}
