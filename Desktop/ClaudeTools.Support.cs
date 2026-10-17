using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's support check of the Workshop's building (support_check).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>support_check: the Workshop's support check, the pieces that would fall and the weakest still standing.</summary>
    [McpServerTool(Name = "support_check", ReadOnly = true, Title = "Would it stand?")]
    [Description("The Workshop's support check, as the game computes it: for the building pieces on the plot, which would fall (nothing holds them up enough) and the weakest ones still standing (support 0 about to fall, 1 strong). Pieces on the ground hold best; support drops with each piece away from it (wood less far than stone, iron further).")]
    public Task<string> SupportCheck() => OnUi(() =>
    {
        if (!_w.InWorkshop)
        {
            return "The support check is the Workshop's: open_workshop first.";
        }
        // The read the pieces' indices are of, taken before they are worked out.
        int reads = _w.Session?.Scene.Reads ?? 0;
        if (_w.Session is not { } session)
        {
            return "No building piece on the plot.";
        }
        // Worked out without turning the user's Support switch on (a read-only check).
        var (r, things) = _w.SolveSupport(session);
        var scene = session.Scene;
        object Piece(int k) => new
        {
            id = things[k],
            name = _w.NameOfPrefab(scene.Things[things[k]].Prefab),
            x = MathF.Round(scene.Things[things[k]].Position.X, 2),
            y = MathF.Round(scene.Things[things[k]].Position.Y, 2),
            z = MathF.Round(scene.Things[things[k]].Position.Z, 2),
            support = MathF.Round(r.Colour[k], 2),
        };
        var falling = r.Falls.Take(60).Select(Piece).ToList();
        // Support: 0 (red, about to fall) to 1 (green); pieces on the ground (light blue in the editor) hold best and are left out.
        var weakest = Enumerable.Range(0, things.Count).Where(k => !r.Free[k] && !r.Breaks[k] && r.Colour[k] >= 0).OrderBy(k => r.Colour[k]).Take(10).Select(Piece).ToList();
        if (falling.Count > 0 || weakest.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(new
        {
            pieces = things.Count,
            wouldFall = r.Breaking,
            falling,
            weakest,
            onTheGround = Enumerable.Range(0, things.Count).Count(k => r.Colour[k] < 0),
        });
    });
}
