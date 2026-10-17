using System.Text;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Characters read from the game's folders, and the default builder of a world.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderCharactersTests : HomeFolderTestBase
{
    /// <summary>
    /// A character file as the game writes it, around the bits that are read: name, id, seed.
    /// </summary>
    private static byte[] Fch(string name, long id, string seed = "AbCdEfGh", byte[]? before = null)
    {
        var b = new List<byte>(before ?? [0x26, 0x00, 0x00, 0x00, 0x05])
        {
            (byte)Encoding.UTF8.GetByteCount(name)
        };
        b.AddRange(Encoding.UTF8.GetBytes(name));
        b.AddRange(BitConverter.GetBytes(id));
        b.Add((byte)seed.Length);
        b.AddRange(Encoding.ASCII.GetBytes(seed));
        b.AddRange(new byte[16]);
        return [.. b];
    }

    [Fact]
    public void CharactersAreReadFromTheGamesFolders()
    {
        string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
        Directory.CreateDirectory(chars);
        File.WriteAllBytes(Path.Combine(chars, "ragnar.fch"), Fch("Ragnar", 1234567890123));
        // The same character twice (a backup copy) counts once.
        File.WriteAllBytes(Path.Combine(chars, "ragnar_copy.fch"), Fch("ragnar_copy", 1234567890123));
        File.WriteAllBytes(Path.Combine(chars, "junk.fch"), [1, 2, 3]);
        var list = Characters.Local();
        var r = Assert.Single(list);
        Assert.Equal("Ragnar", r.Name);
        Assert.Equal(1234567890123, r.Id);
        Assert.Contains(Characters.Roots(), d => d == chars);
        Assert.Contains(Characters.Roots(), d => d.EndsWith(Path.Combine("Valheim", "characters"), StringComparison.Ordinal));
    }

    /// <summary>
    /// Writes Ragnar's character file, with this id, into the game's characters folder.
    /// </summary>
    /// <returns>The file's path.</returns>
    private string WriteRagnarCharacter(long id)
    {
        string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
        Directory.CreateDirectory(chars);
        string file = Path.Combine(chars, "ragnar.fch");
        File.WriteAllBytes(file, Fch("Ragnar", id));
        return file;
    }

    [Fact]
    public void ACharacterFileIsReadAgainOnlyWhenItChanged()
    {
        string file = WriteRagnarCharacter(5);
        Assert.Equal(5, Assert.Single(Characters.Local()).Id);
        int reads = Characters.FileReads;
        Assert.Equal(5, Assert.Single(Characters.Local()).Id);
        Assert.Equal(reads, Characters.FileReads);
        // Saved again by the game: read again.
        File.WriteAllBytes(file, Fch("Ragnar", 6));
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(6, Assert.Single(Characters.Local()).Id);
        Assert.Equal(reads + 1, Characters.FileReads);
    }

    [Fact]
    public void EachThreadCountsOnlyTheCharacterFilesItRead()
    {
        // The count is the thread's own (the tests read characters on other threads at the same time).
        string file = WriteRagnarCharacter(5);
        Assert.Equal(5, Assert.Single(Characters.Local()).Id);
        int reads = Characters.FileReads;
        File.WriteAllBytes(file, Fch("Ragnar", 6));
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        // Read on another thread, asserted here: a failed assert there would end the test run, not fail the test.
        List<Characters.Character>? otherList = null;
        int otherReads = -1;
        var other = new Thread(() =>
        {
            otherList = Characters.Local();
            otherReads = Characters.FileReads;
        });
        other.Start();
        other.Join();

        // The other thread read the changed file: its count only.
        Assert.Equal(6, Assert.Single(otherList!).Id);
        Assert.Equal(1, otherReads);
        Assert.Equal(reads, Characters.FileReads);
        // Here, the file unchanged since the other thread read it: not read again.
        Assert.Equal(6, Assert.Single(Characters.Local()).Id);
        Assert.Equal(reads, Characters.FileReads);
    }

    [Fact]
    public void ACharacterFileThatCannotBeReadIsPassedOver()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
        Directory.CreateDirectory(chars);
        File.WriteAllBytes(Path.Combine(chars, "secret.fch"), Fch("Secret", 9));
        File.SetUnixFileMode(Path.Combine(chars, "secret.fch"), UnixFileMode.None);
        File.WriteAllBytes(Path.Combine(chars, "open.fch"), Fch("Open", 10));
        Assert.Equal(new[] { 10L }, Characters.Local().Select(c => c.Id).ToArray());
    }

    [Fact]
    public void ACharacterFileIsReadOnlyWhereNameIdAndSeedFit()
    {
        Assert.Equal(new Characters.Character("Bjorn", 42), Characters.Read(Fch("Bjorn", 42), "bjorn"));
        // The name in the file may differ in case from the file name.
        Assert.Equal("BJORN", Characters.Read(Fch("BJORN", 42), "bjorn")!.Name);
        Assert.Null(Characters.Read(Fch("Bjorn", 0), "bjorn"));
        Assert.Null(Characters.Read(Fch("Bjorn", 42, seed: "bad\u0001seed"), "bjorn"));
        Assert.Null(Characters.Read(Fch("Bjorn", 42, seed: new string('s', 40)), "bjorn"));
        Assert.Null(Characters.Read(Fch("Bjorn", 42), "freya"));
        Assert.Null(Characters.Read(Fch("Bjorn", 42), ""));
        Assert.Null(Characters.Read(Fch("Bjorn", 42), new string('x', 200)));
        // A first match that does not fit (no id) is skipped for a later one that does.
        byte[] twice = [.. Fch("Bjorn", 0), .. Fch("Bjorn", 77)];
        Assert.Equal(77, Characters.Read(twice, "bjorn")!.Id);
    }

    [Fact]
    public void TheDefaultBuilderIsThisComputersCharacterWhenTheWorldHasNone()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        if (world.TopBuilder != 0)
        {
            Assert.Equal(world.TopBuilder, Builders.Default(world));
            return;
        }
        long expected = world.PlayerNames.Count > 0 ? world.PlayerNames.Keys.First() : Builders.Unknown;
        Assert.Equal(expected, Builders.Default(world));
        string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
        Directory.CreateDirectory(chars);
        File.WriteAllBytes(Path.Combine(chars, "freya.fch"), Fch("Freya", 555));
        Assert.Equal(555, Builders.Default(world));
        var me = Assert.Single(Builders.Players(world), p => p.Id == 555);
        Assert.True(me.Local);
        Assert.Equal("Freya (this computer)", me.Label);
    }
}
