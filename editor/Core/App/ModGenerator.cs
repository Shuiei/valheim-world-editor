using TerrainEditor.Save;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>A world file's generator header, as its generator read it: the generator and the world's settings.</summary>
/// <param name="Generator">The generator.</param>
/// <param name="Settings">The world's settings.</param>
public sealed record GeneratorHeader(ModGenerator Generator, GeneratorSettings Settings);

/// <summary>
/// A world generator an extension registered (IWorldGenerator), called as the editor needs it: what it throws with why
/// for the player (InvalidDataException reading a header, InvalidOperationException getting ready) is passed on as such;
/// anything else is a fault of its extension, which is turned off (<paramref name="fault"/>), said in the log, and told
/// to the player as the add-on having failed.
/// </summary>
/// <param name="generator">The generator.</param>
/// <param name="fault">Turns its extension off after a fault (with why); null for none to turn off.</param>
public sealed class ModGenerator(IWorldGenerator generator, Action<Exception>? fault)
{
    /// <summary>The id its worlds' files name.</summary>
    public string Id { get; } = generator.Id;

    /// <summary>The mod's plugin id.</summary>
    public string PluginId { get; } = generator.PluginId;

    /// <summary>The mod's name.</summary>
    public string Name { get; } = generator.Name;

    /// <summary>
    /// Reads what follows the id in a world file. Throws <see cref="GeneratorHeaderException"/>, with why for the player,
    /// when the generator cannot (the world is not opened); a file that ends early throws as it does.
    /// </summary>
    /// <param name="pkg">The world file's package, just after the id.</param>
    /// <param name="version">The game's file version.</param>
    public GeneratorSettings ReadHeader(ValheimReader pkg, out int version)
    {
        try
        {
            (var settings, version) = Call<(GeneratorSettings, int), InvalidDataException>(
                () => (generator.ReadHeader(new FileReader(pkg), out int v), v), "reading a world file's header", why => throw new GeneratorHeaderException(why));
            return settings;
        }
        catch (InvalidDataException e)
        {
            throw new GeneratorHeaderException(e.Message);
        }
    }

    /// <summary>Writes what follows the id in the file of a world it makes.</summary>
    /// <param name="pkg">The world file's package, just after the id.</param>
    /// <param name="version">The game's file version.</param>
    /// <param name="settings">The world's settings.</param>
    public void WriteHeader(BinaryWriter pkg, int version, GeneratorSettings settings) =>
        Call<bool, IOException>(() =>
        {
            generator.WriteHeader(new FileWriter(pkg), version, settings);
            return true;
        }, "writing a world file's header", why => throw new InvalidOperationException(why));

    /// <summary>Why a world with these settings cannot be drawn, as far as can be told quickly; null when it may be.</summary>
    /// <param name="settings">The world's settings.</param>
    public string? Problem(GeneratorSettings settings) => Call<string?, OutOfMemoryException>(() => generator.Problem(settings), "checking a world", why => why);

    /// <summary>
    /// The rules a world with these settings is laid out with. Throws <c>InvalidOperationException</c>, with why for the
    /// player, when the generator cannot get ready.
    /// </summary>
    /// <param name="settings">The world's settings.</param>
    /// <param name="gamePluginVersion">The mod's version a live game runs; null when not live, or unknown.</param>
    public GeneratorRules Rules(GeneratorSettings settings, string? gamePluginVersion)
    {
        var layout = Call<IWorldLayout, InvalidOperationException>(() => generator.Open(settings, gamePluginVersion), "getting ready to lay out a world", why => throw new InvalidOperationException(why));
        return new GeneratorRules(Id, Name, layout.Version, settings.Description)
        {
            LayOut = seed => new Guarded(Call<IGroundLayout, InvalidOperationException>(() => layout.LayOut(seed), $"laying out seed {seed}", why => throw new InvalidOperationException(why)), this),
            Slow = layout.Slow,
            Header = new GeneratorHeader(this, settings),
        };
    }

    /// <summary>The settings a new world gets; null when the generator makes none, or cannot tell (said in the log).</summary>
    public GeneratorSettings? NewWorldSettings()
    {
        try
        {
            return Call<GeneratorSettings?, InvalidOperationException>(generator.NewWorldSettings, "giving a new world's settings", _ => null);
        }
        catch (InvalidOperationException e)
        {
            Log.Caught(e, $"asking {Name} for a new world's settings (new worlds are made as the game makes them without it)");
            return null;
        }
    }

    /// <summary>What Expand World's settings do otherwise to a world it made; none when the generator fails at it (said in the log).</summary>
    /// <param name="settings">The world's settings.</param>
    /// <param name="expandWorld">The world's Expand World settings.</param>
    public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) =>
        Call<IReadOnlyList<string>, OutOfMemoryException>(() => generator.Notes(settings, expandWorld), "telling what Expand World does to a world", _ => []);

    /// <summary>
    /// Calls the generator: <typeparamref name="TPassed"/> (what it throws with why for the player; OutOfMemoryException
    /// where it throws none so) is passed on, and so is a file that ends early or running out of memory; anything else
    /// is a fault (<see cref="Faulted"/>), whose message for the player <paramref name="faulted"/> turns into the answer
    /// (or throws).
    /// </summary>
    private T Call<T, TPassed>(Func<T> call, string doing, Func<string, T> faulted)
        where TPassed : Exception
    {
        try
        {
            return call();
        }
        catch (Exception e) when (e is not (TPassed or EndOfStreamException or OutOfMemoryException))
        {
            return faulted(Faulted(e, doing));
        }
    }

    /// <summary>Logs a fault of the generator, turns its extension off, and says it for the player.</summary>
    private string Faulted(Exception e, string doing)
    {
        Log.Caught(e, $"{Name}'s world generator {doing} (its extension is turned off)");
        fault?.Invoke(e);
        return Lang.T("worldgen.failed", Name);
    }

    /// <summary>
    /// A laid-out world's answers for points, a fault among them handled as any of the generator's (its extension turned
    /// off, said in the log), then thrown as <c>InvalidOperationException</c> with why for the player: there is no answer
    /// to give instead. No closure per point: the map asks millions.
    /// </summary>
    private sealed class Guarded(IGroundLayout ground, ModGenerator owner) : IGroundLayout
    {
        public int Biome(float x, float z)
        {
            try
            {
                return ground.Biome(x, z);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw owner.PointFault(e);
            }
        }

        public float Height(float x, float z, out System.Numerics.Vector4 mask)
        {
            try
            {
                return ground.Height(x, z, out mask);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw owner.PointFault(e);
            }
        }

        public float Forest(float x, float z)
        {
            try
            {
                return ground.Forest(x, z);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw owner.PointFault(e);
            }
        }
    }

    /// <summary>A fault of a laid-out world answering for a point, handled and made the exception thrown in its place.</summary>
    private InvalidOperationException PointFault(Exception e) => new(Faulted(e, "answering for a point of its world"), e);

    /// <summary>A world file's package as a generator reads its header.</summary>
    private sealed class FileReader(ValheimReader r) : IWorldFileReader
    {
        public int ReadInt() => r.ReadInt();

        /// <summary>A string; a damaged length prefix is damaged data (InvalidDataException), not the generator's fault.</summary>
        public string ReadString()
        {
            try
            {
                return r.ReadString();
            }
            catch (FormatException e)
            {
                throw new InvalidDataException(e.Message, e);
            }
        }

        public byte ReadByte() => r.ReadByte();

        public int ReadCount() => r.ReadNumItems(WorldVersion.NumItems);

        public long ReadLong() => r.ReadLong();

        public float ReadSingle() => r.ReadSingle();

        public bool ReadBool() => r.ReadBool();
    }

    /// <summary>A world file's package as a generator writes its header.</summary>
    private sealed class FileWriter(BinaryWriter w) : IWorldFileWriter
    {
        public void WriteInt(int value) => w.Write(value);

        public void WriteString(string value) => w.Write(value);

        public void WriteByte(byte value) => w.Write(value);

        public void WriteLong(long value) => w.Write(value);

        public void WriteSingle(float value) => w.Write(value);

        public void WriteBool(bool value) => w.Write(value);
    }
}
