using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The kinds regrow_nature grows, from their names (all four when none is given), or the first name that is none of them.</summary>
    /// <param name="kinds">The kinds' names, or null for all four.</param>
    internal static (HashSet<ObjectKind>? Kinds, string? Problem) KindsOf(string[]? kinds)
    {
        var chosen = new HashSet<ObjectKind>();
        foreach (string k in kinds is { Length: > 0 } ? kinds : ["Trees", "Rocks", "Bushes", "Pickables"])
        {
            if (!Enum.TryParse<ObjectKind>(k, true, out var kind) || kind is not (ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Bushes or ObjectKind.Pickables))
            {
                return (null, $"{k}: the kinds are Trees, Rocks, Bushes and Pickables.");
            }
            chosen.Add(kind);
        }
        return (chosen, null);
    }

    /// <summary>regrow_nature: what the game grows in a rectangle (Growth), of the kinds asked, where nothing stands.</summary>
    [McpServerTool(Name = "regrow_nature", Title = "Regrow nature")]
    [Description("Regrows nature over a rectangle of the open area of a world, as the Area tool's Regrow nature does: the trees, rocks, bushes and pickables the game itself grows there from the world's seed (its kinds, sizes and places, for the biome), left out where something stands (anything within 1 m, the same kind within 3 m, buildings within 4 m). Use it after clearing or reshaping ground to make it look untouched. Up to 16 zones (256 x 256 m) at once. One pending step.")]
    public async Task<string> RegrowNature(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Kinds to grow: Trees, Rocks, Bushes, Pickables (default: all four).")] string[]? kinds = null)
    {
        var (chosen, kindProblem) = KindsOf(kinds);
        if (chosen == null)
        {
            return kindProblem!;
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
            o => o.X >= ax && o.X <= bx && o.Z >= az && o.Z <= bz && chosen.Contains(ObjectKinds.Of(o.Name, false)),
            _w.NameOfPrefab, m => said = m.English));
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
            Growth.Commit(session, ClaudesStep(Lang.T("claude.undo.regrew", spots.Count)), spots);
            _w.AfterClaudeEdit(Said.Of("claude.msg.regrew", spots.Count));
            var counts = spots.GroupBy(o => o.Name).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}");
            return $"Regrew {spots.Count} object(s): {string.Join(", ", counts.Take(12))}. Now pending: {Pending()}";
        });
    }
}
