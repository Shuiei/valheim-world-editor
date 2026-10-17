using Xunit;
using static TerrainEditor.Desktop.Tests.ClaudePieces;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The blueprint piece builder, tested once here so the tests using it can rely on it.
/// </summary>
public sealed class ClaudePiecesTests
{
    [Fact]
    public void APieceCarriesItsPrefabOffsetsAndHeightAndIsNotTurned()
    {
        var p = Piece("wood_pole2", -6, 1);
        Assert.Equal("wood_pole2", p["name"]!.GetValue<string>());
        Assert.Equal(-6, p["dx"]!.GetValue<float>());
        Assert.Equal(1, p["dy"]!.GetValue<float>());
        Assert.Equal(0, p["rx"]!.GetValue<int>());
    }
}
