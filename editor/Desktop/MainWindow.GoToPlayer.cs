
namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Go to a player: right here when they are in the area, else the area around them.</summary>
    internal async Task GoToPlayer(string? name)
    {
        if (name == null || View.Scene is not { } s || LivePlayers.FirstOrDefault(p => p.Name == name) is not { Name: not null } p)
        {
            return;
        }
        float gx = p.World.X - s.OriginX, gz = p.World.Z - s.OriginZ;
        if (!(gx > 2 && gz > 2 && gx < s.W - 3 && gz < s.H - 3) && s.Owner != null)
        {
            if (MoveBlocker() is { } why)
            {
                Msg("live.msg.cannotGo", name, why);
                return;
            }
            var (zx, zz) = TerrainEditor.Save.ChunkMath.SectorOf(p.World.X, p.World.Z);
            await EditBlock(zx - s.ZonesX / 2, zz - s.ZonesZ / 2, s.ZonesX, s.ZonesZ);
        }
        View.Focus(p.World);
        Msg("live.msg.lookingAt", name);
    }
}
