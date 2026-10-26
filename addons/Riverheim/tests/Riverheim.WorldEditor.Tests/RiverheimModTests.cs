using Xunit;

namespace Riverheim.WorldEditor.Tests;

/// <summary>
/// Riverheim's header in a world file (after its id): its preset and the game's file version read, written as Riverheim
/// writes it, a setting the add-on does not know or no preset refused with why; which of its versions lay worlds out
/// alike.
/// </summary>
public class RiverheimModTests
{
    /// <summary>A header as Riverheim writes it, with these settings.</summary>
    private static byte[] Header(params (string Name, object Value)[] settings) => WorldFile.AsTheGameWrites(w =>
    {
        w.Write(36);
        w.Write((byte)settings.Length);
        foreach (var (name, value) in settings)
        {
            w.Write(name);
            if (value is int i)
            {
                w.Write(i);
            }
            else
            {
                w.Write((string)value);
            }
        }
    });

    [Fact]
    public void AHeaderIsReadAndWrittenAsRiverheimWritesIt()
    {
        byte[] amur = Header(("preset", "builtin.amur"), ("preset.version", 2));
        Assert.Equal(new RiverheimMod.Tag("builtin.amur", 2), RiverheimMod.ReadHeader(WorldFile.Reader(amur), out int version));
        Assert.Equal(36, version);
        Assert.Equal(amur, WorldFile.Written(w => RiverheimMod.WriteHeader(w, 36, new RiverheimMod.Tag("builtin.amur", 2))));
    }

    [Fact]
    public void ASettingItDoesNotKnowOrNoPresetCannotBeRead()
    {
        // A setting's value has the size its name gives: an unknown name ends the read.
        Assert.Equal(Texts.UnknownSetting("made.up"), Assert.Throws<InvalidDataException>(() => RiverheimMod.ReadHeader(WorldFile.Reader(Header(("preset", "builtin.amur"), ("made.up", 3))), out _)).Message);
        Assert.Equal(Texts.NoPreset, Assert.Throws<InvalidDataException>(() => RiverheimMod.ReadHeader(WorldFile.Reader(Header(("preset.version", 2))), out _)).Message);
        Assert.Equal(Texts.NoPreset, Assert.Throws<InvalidDataException>(() => RiverheimMod.ReadHeader(WorldFile.Reader(Header(("preset", "builtin.amur"))), out _)).Message);
    }

    [Theory]
    [InlineData("1.2.1", "1.2.0", true)]
    [InlineData("1.2.1", "1.2.1", true)]
    [InlineData("1.1.1", "1.2.1", false)]
    [InlineData("2.2.1", "1.2.1", false)]
    [InlineData("beta", "1.2.1", false)]
    public void ARiverheimReleaseIsItsMajorAndMinorVersion(string a, string b, bool same) => Assert.Equal(same, RiverheimMod.SameRelease(a, b));
}
