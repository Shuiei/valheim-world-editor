using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Light.
    /// <summary>
    /// A random stream of the room's own for one matter (lights, decay), the same whatever the settings for the
    /// others: so that turning the light down or the decay off changes nothing else about the dungeon.
    /// </summary>
    private Random Local(Frame f, int salt) => new(unchecked(_s.Seed * 7919 + f.S.Id * 104729 + salt));

    /// <summary>
    /// How much more a room holds for its depth: a fifth of the top level's foes and loot more for each level down
    /// (linear, not compounding: over sixteen levels the bottom holds four times the top, not fifteen), as dungeons
    /// grow richer and more dangerous the deeper they go.
    /// </summary>
    private static float Depth(Frame f) => 1 + 0.2f * f.S.Level;

    /// <summary>
    /// How a room's purpose lights it: the grand rooms (the entrance, the throne room, the treasury, the feast hall, a
    /// chapel) closer lit, the rooms of the dead and the cells dimmer (and in the dark, Light 0, never lit at all).
    /// </summary>
    private static float LightOf(Room room) => room switch
    {
        Room.Entrance or Room.Throne or Room.Treasury or Room.Feast or Room.Chapel or Room.Boss => 0.75f,
        Room.Ossuary or Room.Tomb or Room.Burial or Room.Cells or Room.Torture or Room.Den or Room.Secret or Room.Store => 1.5f,
        _ => 1,
    };

    /// <summary>The biome's standing torch; a castle torch is now and then unlit in a decayed dungeon (by rnd's chance).</summary>
    private string TorchOf(Random rnd) => _decay > 0 && _b.Torch.StartsWith("CastleKit_groundtorch", StringComparison.Ordinal) && rnd.NextDouble() < _decay * 0.5 ? "CastleKit_groundtorch_unlit" : _b.Torch;

    /// <summary>The biome's brazier at (u, v), if the spot is free (a light's brazier keeps no room for itself: see <see cref="PlaceLights"/>).</summary>
    private void Brazier(Frame f, float u, float v, bool take = true) => Put(f, _b.Brazier, u, v, 0, 0.6f, force: false, take: take);

    /// <summary>Two braziers at the back corners of the room, either side of its middle.</summary>
    private void BackBraziers(Frame f)
    {
        Brazier(f, -(f.HalfA - 1.1f), -(f.HalfD - 1.1f));
        Brazier(f, f.HalfA - 1.1f, -(f.HalfD - 1.1f));
    }

    /// <summary>
    /// Asks for the room's lights (torches along the walls, closer the brighter the settings' Light; in the dark,
    /// Light 0, most rooms get none unless important). They are placed once the room is furnished.
    /// </summary>
    private void Lights(bool important = false) => _lights = Math.Max(_lights, important ? 2 : 1);

    /// <summary>
    /// The room's lights, after everything else: they take free spots but keep none, so that the settings' Light
    /// changes nothing but them (their stream is the room's own, their spots never move what comes later).
    /// </summary>
    private void PlaceLights(Frame f, bool important)
    {
        var rnd = Local(f, 1);
        if (_s.Light <= 0 && !important && (LightOf(f.S.Room) > 1 || rnd.NextDouble() >= 0.3))
        {
            return;
        }
        float step = (_s.Light >= 2 ? 5 : _s.Light == 1 ? 8 : 12) * LightOf(f.S.Room);
        // How the room is lit: standing torches along the walls, sconces on them, or a brazier or two in the corners.
        switch (rnd.Next(5))
        {
            case 0 or 1 when _mat.Name is "Stone" or "Marble" or "Grausten":
                WallTorches(f, step);
                break;
            case 2 when f.Area >= 40:
                foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => rnd.Next()).Take(_s.Light >= 2 ? 4 : 2))
                {
                    var (bu, bv) = In((u, v, yaw), 1.1f);
                    Brazier(f, bu, bv, take: false);
                }
                break;
            default:
                string lamp = Lamp(rnd);
                foreach (var s in f.WallSpots(step, 2.5f))
                {
                    var (u, v) = In(s, 0.7f);
                    Put(f, lamp, u, v, s.Yaw, 0.4f, take: false);
                }
                break;
        }
    }
}
