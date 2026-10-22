using TerrainEditor.Save;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The world generation mods the editor draws worlds of (IWorldGenerator): those the running extensions register (an
/// add-on's, a mod's own; Riverheim's comes as the Riverheim add-on). A world file marks a world one of them made: -1
/// where the game's file version goes, then the generator's id, then its header.
/// </summary>
public static class WorldGenerators
{
    /// <summary>What a world file made with a world generation mod starts with where the game's file version would be (always positive).</summary>
    public const int Marker = -1;

    /// <summary>
    /// The generator a world file names; null when none in use does. The extensions are run first when which run has not
    /// been decided yet (<see cref="Decide"/>).
    /// </summary>
    /// <param name="id">The generator's id.</param>
    public static ModGenerator? Find(string id)
    {
        if (Of(id) is { } found)
        {
            return found;
        }
        Decide(AppSettings.Load());
        return Of(id);
    }

    /// <summary>
    /// Runs the extensions once, when which run has not been decided yet (the start page and the New world page come
    /// before any world opens): their generators are then in use.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    private static void Decide(AppSettings settings)
    {
        if (!ModExtensions.Decided)
        {
            ModManifests.Refresh(settings);
            ModExtensions.Refresh(settings);
        }
    }

    /// <summary>The generator in use with this id.</summary>
    private static ModGenerator? Of(string id) => ModExtensions.Generators.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// The world file's version, and the generator header before it in a world a world generation mod made (null for
    /// any other). Throws <see cref="GeneratorHeaderException"/>, with why for the player, for a generator not in use or
    /// a header it cannot read.
    /// </summary>
    /// <param name="pkg">The world file's package, at its start.</param>
    /// <param name="header">Set to the generator's header, or null.</param>
    public static int ReadVersion(ValheimReader pkg, out GeneratorHeader? header)
    {
        int version = pkg.ReadInt();
        header = null;
        if (version != Marker)
        {
            return version;
        }
        string id = pkg.ReadString();
        var generator = Find(id) ?? throw new GeneratorHeaderException(WhyNone(id));
        header = new GeneratorHeader(generator, generator.ReadHeader(pkg, out version));
        return version;
    }

    /// <summary>
    /// Why a world whose file names a generator none in use registers cannot open, for the player: the add-on that
    /// says it draws it (its manifest's "worldGenerators") is turned off, waits for its mod, or is not running; or a
    /// mod's own extension that says so is not turned on, or does not run; else no add-on the editor knows draws it.
    /// </summary>
    /// <param name="id">The generator's id, as the world file names it.</param>
    internal static string WhyNone(string id)
    {
        if (ModManifests.InstalledAddOns.FirstOrDefault(a => a.WorldGenerators?.ContainsKey(id) == true) is { } a)
        {
            string mod = a.WorldGenerators![id];
            return !a.On ? Lang.T("worldgen.addOnOff", mod, a.Name)
                : !a.Served ? Lang.T("worldgen.addOnWaiting", mod, a.Name, a.Serves)
                : Lang.T("worldgen.addOnNotRunning", mod, a.Name);
        }
        if (ModManifests.Extensions.FirstOrDefault(o => !o.AddOn && o.WorldGenerators?.ContainsKey(id) == true) is not { } x)
        {
            return Lang.T("worldgen.unknown", id);
        }
        bool off = ModExtensions.Offered.FirstOrDefault(e => e.Dll == x.Dll) is not { } e || e.State == ModExtensions.Status.Off;
        return Lang.T(off ? "worldgen.extensionOff" : "worldgen.extensionNotRunning", x.WorldGenerators![id], x.Mod);
    }

    /// <summary>Writes where a new world's file version goes: the version, or the generator's marker, id and header.</summary>
    /// <param name="pkg">The world file's package, at its start.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="header">The generator's header; null for a world the game makes.</param>
    public static void WriteVersion(BinaryWriter pkg, int version, GeneratorHeader? header)
    {
        if (header == null)
        {
            pkg.Write(version);
            return;
        }
        pkg.Write(Marker);
        pkg.Write(header.Generator.Id);
        header.Generator.WriteHeader(pkg, version, header.Settings);
    }

    /// <summary>
    /// The rules a new world is made and looked at with (the New world page): the world rules providers' for a new world
    /// (WorldRulesSource), with the
    /// first generator in use that makes new worlds (as the game makes every new world with the mod while it is
    /// installed); the game's own when none does, or the generator cannot get ready (said in the log). The extensions are
    /// run first when which run has not been decided yet.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules ForNewWorld(AppSettings settings)
    {
        Decide(settings);
        var rules = WorldRulesSource.ForNewWorld(settings);
        foreach (var g in ModExtensions.Generators)
        {
            if (g.NewWorldSettings() is not { } made)
            {
                continue;
            }
            try
            {
                var generator = g.Rules(made, null);
                Console.WriteLine($"{g.Name}: new worlds are laid out by {g.Name} {generator.Version}, {made.Description}.");
                return rules with { Generator = generator };
            }
            catch (InvalidOperationException e)
            {
                Log.Caught(e, $"using {g.Name} for new worlds (they are made as the game makes them without it)");
            }
        }
        return rules;
    }

    /// <summary>The world's Expand World settings, as a world generator is told them.</summary>
    /// <param name="r">The world's rules.</param>
    public static ExpandWorldSettings ExpandWorldOf(WorldRules r) => new()
    {
        Stretch = r.Stretch,
        BiomeStretch = r.BiomeStretch,
        Radius = r.TotalRadius,
        ShapesGround = r.AltitudeMultiplier != 1f || r.AltitudeDelta != 0f || r.OffsetX != null || r.OffsetY != null || r.HeightSeed != null,
        WorldData = r.World != null,
        Rivers = r.RiversInstalled,
    };
}
