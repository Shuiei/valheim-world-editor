using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The editor's own #EditorData lines of a blueprint file (BlueprintFormats.ReadDataLine and AttachData): a line's field
/// is attached to the piece on its line, and a ruin's pieces get the no-builder field.
/// </summary>
public class BlueprintFormatsEditorDataTests
{
    /// <summary>An #EditorData line for the piece on line n, with a "floats" field of key 7 (value "2" unless given).</summary>
    private static string DataLine(int n, string value = "Mg==") => $"#EditorData:{n};floats;7;{value}";

    /// <summary>A piece with no data yet.</summary>
    private static BlueprintFormats.Piece Piece(string name) => new(name, Vector3.Zero, Vector3.Zero, 1f);

    [Fact]
    public void AWellFormedLineAddsItsFieldToItsPieceLine()
    {
        var data = new Dictionary<int, List<ObjectField>>();
        BlueprintFormats.ReadDataLine(DataLine(2), data);
        Assert.Equal(new ObjectField("floats", 7, "2"), Assert.Single(data[2]));
    }

    [Fact]
    public void TwoLinesForOnePieceKeepBothFields()
    {
        var data = new Dictionary<int, List<ObjectField>>();
        BlueprintFormats.ReadDataLine(DataLine(0), data);
        BlueprintFormats.ReadDataLine("#EditorData:0;ints;8;Mw==", data);
        Assert.Equal(2, data[0].Count);
    }

    [Fact]
    public void ALineWithAnUnreadableIndexAddsNothing()
    {
        var data = new Dictionary<int, List<ObjectField>>();
        BlueprintFormats.ReadDataLine("#EditorData:x;floats;7;Mg==", data);
        Assert.Empty(data);
    }

    [Fact]
    public void ALineWithAnUnknownSectionAddsNothing()
    {
        var data = new Dictionary<int, List<ObjectField>>();
        BlueprintFormats.ReadDataLine("#EditorData:0;nope;7;Mg==", data);
        Assert.Empty(data);
    }

    [Fact]
    public void EachPieceGetsTheFieldsOfItsLine()
    {
        var pieces = new List<BlueprintFormats.Piece> { Piece("a"), Piece("b") };
        var data = new Dictionary<int, List<ObjectField>> { [1] = [new ObjectField("floats", 7, "2")] };
        BlueprintFormats.AttachData(pieces, [0, 1], data, ruin: false);
        Assert.Empty(pieces[0].Data!);
        Assert.Equal(new ObjectField("floats", 7, "2"), Assert.Single(pieces[1].Data!));
    }

    [Fact]
    public void ARuinGivesEveryPieceTheNoBuilderField()
    {
        var pieces = new List<BlueprintFormats.Piece> { Piece("a") };
        BlueprintFormats.AttachData(pieces, [0], [], ruin: true);
        Assert.Equal(ObjectField.NoBuilder, Assert.Single(pieces[0].Data!));
    }

    [Fact]
    public void WithoutDataOrARuinThePiecesAreLeftAsTheyWere()
    {
        var pieces = new List<BlueprintFormats.Piece> { Piece("a") };
        BlueprintFormats.AttachData(pieces, [0], [], ruin: false);
        Assert.Null(pieces[0].Data);
    }
}
