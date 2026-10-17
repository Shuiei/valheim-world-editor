using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether a model entry of the catalogue is a model: a size of three numbers, and parts each with a mesh, a vertex
/// count, a submesh index, a 16-number matrix, and optionally a material name and six bounds numbers.
/// </summary>
public sealed class ModelEntryIsModelTests
{
    /// <summary>An identity matrix of sixteen numbers, as a part's matrix is written.</summary>
    private const string Matrix = "[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]";

    /// <summary>Calls the catalogue's model check.</summary>
    private static bool IsModel(string json) =>
        (bool)PrivateMember.Call(typeof(ModCatalogue), "IsModel", null, JsonNode.Parse(json))!;

    /// <summary>An entry with a size and one complete part is a model.</summary>
    [Fact]
    public void AnEntryWithASizeAndACompletePartIsAModel() =>
        Assert.True(IsModel($"{{\"s\":[1,2,3],\"p\":[{{\"mesh\":\"m\",\"v\":3,\"sub\":0,\"m\":{Matrix}}}]}}"));

    /// <summary>A size of two numbers, not three, is not a model.</summary>
    [Fact]
    public void ASizeOfTwoNumbersIsNotAModel() =>
        Assert.False(IsModel($"{{\"s\":[1,2],\"p\":[{{\"mesh\":\"m\",\"v\":3,\"sub\":0,\"m\":{Matrix}}}]}}"));

    /// <summary>A vertex count that is not a whole number is not a model.</summary>
    [Fact]
    public void AFractionalVertexCountIsNotAModel() =>
        Assert.False(IsModel($"{{\"s\":[1,2,3],\"p\":[{{\"mesh\":\"m\",\"v\":1.5,\"sub\":0,\"m\":{Matrix}}}]}}"));

    /// <summary>A part with a matrix of fifteen numbers is not a model.</summary>
    [Fact]
    public void AShortMatrixIsNotAModel() =>
        Assert.False(IsModel("{\"s\":[1,2,3],\"p\":[{\"mesh\":\"m\",\"v\":3,\"sub\":0,\"m\":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0]}]}"));

    /// <summary>A material name that is not text is not a model.</summary>
    [Fact]
    public void AMaterialThatIsNotTextIsNotAModel() =>
        Assert.False(IsModel($"{{\"s\":[1,2,3],\"p\":[{{\"mesh\":\"m\",\"v\":3,\"sub\":0,\"mat\":5,\"m\":{Matrix}}}]}}"));

    /// <summary>A bounds list of five numbers, not six, is not a model.</summary>
    [Fact]
    public void BoundsOfFiveNumbersAreNotAModel() =>
        Assert.False(IsModel($"{{\"s\":[1,2,3],\"p\":[{{\"mesh\":\"m\",\"v\":3,\"sub\":0,\"b\":[0,0,0,1,1],\"m\":{Matrix}}}]}}"));

    /// <summary>An entry without parts is not a model.</summary>
    [Fact]
    public void AnEntryWithoutAPartsListIsNotAModel() =>
        Assert.False(IsModel("{\"s\":[1,2,3]}"));
}
