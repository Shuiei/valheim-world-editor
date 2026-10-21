using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The saved selections file as it is read and written: a missing file is none, a file that is not JSON is kept aside, null
/// entries are left out, and what is saved reads back.
/// </summary>
[Collection("Saved selections")]
public sealed class SavedSelectionsStorageTests : SavedSelectionsFolder
{
    private static string FilePath => SavedSelections.PathOverride!;

    private static Dictionary<string, List<SavedSelections.Saved>>? LoadAll() =>
        PrivateInvoke.Call<Dictionary<string, List<SavedSelections.Saved>>?>(typeof(SavedSelections), "LoadAll", null);

    private static bool SaveAll(Dictionary<string, List<SavedSelections.Saved>> all) =>
        PrivateInvoke.Call<bool>(typeof(SavedSelections), "SaveAll", null, all);

    [Fact]
    public void ALoadWithNoFileIsEmpty()
    {
        Assert.Empty(LoadAll()!);
    }

    [Fact]
    public void WhatIsSavedReadsBackPerWorld()
    {
        var all = new Dictionary<string, List<SavedSelections.Saved>>
        {
            ["World"] = [new SavedSelections.Saved("Trees", [new SavedSelections.Item(1, 2f, 3f, 4f)])],
        };

        Assert.True(SaveAll(all));
        var loaded = LoadAll()!;

        Assert.Equal(["World"], loaded.Keys);
        Assert.Equal(["Trees"], loaded["World"].Select(s => s.Name));
        Assert.Equal(2f, loaded["World"][0].Items[0].X);
    }

    [Fact]
    public void AFileThatIsNotJsonIsKeptAsideAndCountsAsEmpty()
    {
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(LoadAll()!);
        Assert.Equal("{ not json", File.ReadAllText(FilePath + ".bad"));
    }

    [Fact]
    public void NullEntriesAreLeftOutOfTheLoad()
    {
        File.WriteAllText(FilePath, """{"World":[{"Name":"Keep","Items":[]},{"Name":null,"Items":[]},{"Name":"NoItems","Items":null}],"Gone":null}""");

        var loaded = LoadAll()!;

        Assert.Equal(["Keep"], loaded["World"].Select(s => s.Name));
        Assert.False(loaded.ContainsKey("Gone"));
    }
}
