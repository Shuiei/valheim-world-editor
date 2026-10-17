namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A corridor's lights: torches along the walls (where there are walls: not where another corridor opens in),
    /// every other spot, closer the brighter; sconces instead, now and then, on stone.
    /// </summary>
    private void CorridorLights(Frame f, Random light)
    {
        // Catacombs and prisons keep some corridors dark between their lit rooms (the contrast is the point), unless Bright.
        if (_style.Name is "Catacombs" or "Prison" && _s.Light < 2 && light.NextDouble() < 0.4)
        {
            return;
        }
        float step = _s.Light >= 2 ? 6 : _s.Light == 1 ? 9 : 16;
        bool sconces = _mat.Name is "Stone" or "Marble" or "Grausten" && light.NextDouble() < 0.4;
        string lamp = Lamp(light);
        int k = light.Next(2);
        foreach (var sp in f.WallSpots(step / 2, 2.5f).Where(sp => sp.Yaw is 90 or -90))
        {
            if (k++ % 2 != 0)
            {
                continue;
            }
            if (sconces)
            {
                var (wu, wv) = In(sp, 0.25f);
                _o.Add("piece_walltorch", f.W(wu, wv, f.Top + 2.1f), f.Yaw(sp.Yaw));
            }
            else
            {
                var (tu, tv) = In(sp, 0.5f);
                Put(f, lamp, tu, tv, sp.Yaw, 0.35f, take: false);
            }
        }
    }
}
