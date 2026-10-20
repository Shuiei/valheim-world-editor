using Avalonia.Threading;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the cursor line adds for the point under the mouse: its territory, its area's alt biomes and the status
/// effects Expand World Data puts on a player there.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>The alt biome layout last worked out, and the generator it is of; null until the first is ready.</summary>
    private (WorldGenerator Gen, SeedLocations.AltBiomeLayout Layout)? _alts;

    /// <summary>The generator whose alt biome layout is being worked out (or was), so that it is asked for once.</summary>
    private WorldGenerator? _altsAsked;

    /// <summary>The last point the pointer was over, null before any.</summary>
    private (float X, float Z)? _hovered;

    /// <summary>Stops the alt biome layout being worked out (another world's asked for, or the page left).</summary>
    private CancellationTokenSource? _altsCancel;

    /// <summary>
    /// The point's territory, alt biomes and status effects, each after " · "; empty when it has none. The alt biomes
    /// show once the world's layout is worked out (asked for the first time a point of the world is shown).
    /// </summary>
    private string SpotExtras(WorldSession session, float x, float z)
    {
        var terrain = session.Terrain;
        return Extras(terrain.TerritoryAt(x, z), AltBiomesAt(terrain.Generator, x, z), terrain.StatusEffectsAt(x, z));
    }

    /// <summary>The cursor line's extras from what a point has (see <see cref="SpotExtras"/>).</summary>
    /// <param name="territory">Its territory, if any.</param>
    /// <param name="alts">Its area's alt biomes; null while not known.</param>
    /// <param name="effects">The status effects there.</param>
    internal static string Extras(Territory? territory, IReadOnlyList<SeedLocations.AltBiomeRule>? alts, IReadOnlyList<StatusEffect> effects)
    {
        List<string> parts = [];
        if (territory != null)
        {
            parts.Add(Lang.T("map.label.territory", territory.Name));
        }
        if (alts is { Count: > 0 })
        {
            parts.Add(Lang.T("map.label.altBiomes", string.Join(", ", alts.Select(a => a.Name))));
        }
        var named = effects.Where(e => e.Name.Trim() != "").ToList();
        if (named.Count > 0)
        {
            parts.Add(Lang.T("map.label.effects", string.Join(", ", named.Select(EffectText))));
        }
        return string.Concat(parts.Select(p => " · " + p));
    }

    /// <summary>A status effect's name, with when it is on unless it is on by day and by night.</summary>
    private static string EffectText(StatusEffect e) => (e.Day, e.Night) switch
    {
        (true, false) => Lang.T("map.label.byDay", e.Name),
        (false, true) => Lang.T("map.label.byNight", e.Name),
        _ => e.Name,
    };

    /// <summary>
    /// The alt biomes of the area holding a point, once the world's layout is worked out; null before (the first ask
    /// starts working it out in the background, after the game's location rules are read).
    /// </summary>
    private IReadOnlyList<SeedLocations.AltBiomeRule>? AltBiomesAt(WorldGenerator gen, float x, float z)
    {
        if (_alts is { } kept && kept.Gen == gen)
        {
            return kept.Layout.At(x, z);
        }
        if (_altsAsked != gen)
        {
            _altsAsked = gen;
            StopAltBiomes();
            _altsCancel = new CancellationTokenSource();
            _ = LayOutAltBiomes(gen, _altsCancel.Token);
        }
        return null;
    }

    /// <summary>Stops the alt biome layout being worked out, if one is.</summary>
    private void StopAltBiomes()
    {
        _altsCancel?.Cancel();
        _altsCancel?.Dispose();
        _altsCancel = null;
    }

    /// <summary>
    /// Works a world's alt biome layout out in the background and keeps it (said in the log when it cannot be, or is
    /// stopped).
    /// </summary>
    private async Task LayOutAltBiomes(WorldGenerator gen, CancellationToken cancel)
    {
        try
        {
            await GameLocations.UseGameRules(GameLook.Bundles).WaitAsync(cancel);
            var layout = await Task.Run(() => SeedLocations.AltBiomesOf(gen, cancel), cancel);
            // Kept only while it is still the one asked for (not stopped, no other world's asked for since).
            Dispatcher.UIThread.Post(() =>
            {
                if (!cancel.IsCancellationRequested && _altsAsked == gen)
                {
                    _alts = (gen, layout);
                    // The line under the pointer now has its alt biomes.
                    if (_hovered is var (x, z))
                    {
                        Hover(x, z);
                    }
                }
            });
        }
        catch (Exception e)
        {
            // Run without a caller to see it: whatever it throws ends here, in the log.
            Log.Caught(e, "laying out the alt biomes for the map's cursor (none shown)");
        }
    }
}
