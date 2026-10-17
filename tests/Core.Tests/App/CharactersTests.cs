using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading a character's name and player id from its file (Characters.Read): names beyond ASCII match their file name
/// ignoring case.
/// </summary>
public class CharactersTests
{
    // The file name is lowered with Unicode rules, the bytes were lowered only for ASCII: "Ørjan" was never found.
    [Fact]
    public void ANameBeyondAsciiMatchesItsFileName()
    {
        byte[] name = Encoding.UTF8.GetBytes("Ørjan");
        byte[] file = [1, 2, 3, (byte)name.Length, .. name, .. BitConverter.GetBytes(42L), .. new byte[] { 0, 1, 0, 0 }];

        Assert.Equal(new Characters.Character("Ørjan", 42), Characters.Read(file, "ørjan"));
    }
}
