using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What set_contents gives an object to hold (ClaudeTools): an item stand's one item (and its refusals), a container's
/// items in their slots (and too many), a sign's text and the refusal of text and stars for other objects.
/// </summary>
[Collection("World files")]
public class ClaudeToolsContentsTests
{
    // An item stand takes one item into its change list, which the change then holds; nothing is left over.
    [AvaloniaFact]
    public void AnItemStandTakesItsItem()
    {
        using var r = new ClaudeRun();
        var set = new List<FieldChange>();
        var (left, refusal) = ClaudeTools.StandChanges(Save.StableHash.Of("itemstand"), "itemstand", [new ClaudeTools.ItemSpec { Item = "Coins" }], null, null, set);
        Assert.Null(left);
        Assert.Null(refusal);
        Assert.NotEmpty(set);
    }

    // An item stand holds one item: two are refused with the number, and nothing is added.
    [AvaloniaFact]
    public void AnItemStandRefusesMoreThanOneItem()
    {
        using var r = new ClaudeRun();
        var set = new List<FieldChange>();
        var (_, refusal) = ClaudeTools.StandChanges(Save.StableHash.Of("itemstand"), "itemstand", [new ClaudeTools.ItemSpec { Item = "Coins" }, new ClaudeTools.ItemSpec { Item = "Coins" }], null, null, set);
        Assert.Equal("itemstand holds one item, not 2.", refusal);
        Assert.Empty(set);
    }

    // The orientation is an item stand's: another object refuses it, and the message says what takes one.
    [AvaloniaFact]
    public void AnOrientationIsRefusedForAnotherObject()
    {
        using var r = new ClaudeRun();
        var set = new List<FieldChange>();
        var (left, refusal) = ClaudeTools.StandChanges(Save.StableHash.Of("Beech1"), "Beech1", null, 1, null, set);
        Assert.Null(left);
        Assert.Equal("Beech1 is not an item stand: only item stands take an orientation.", refusal);
        Assert.Empty(set);
    }

    // A chest's items go into its slots in order, row by row: the second one is the next slot along.
    [AvaloniaFact]
    public void AContainerTakesItsItemsInSlotOrder()
    {
        using var r = new ClaudeRun();
        var (uploads, refusal) = ClaudeTools.ContainerItems(Save.StableHash.Of("piece_chest_wood"), "chest", [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 3 }, new ClaudeTools.ItemSpec { Item = "Wood" }]);
        Assert.Null(refusal);
        Assert.NotNull(uploads);
        Assert.Equal(["Coins", "Wood"], uploads.Select(u => u.Name));
        Assert.Equal(3, uploads[0].Stack);
        Assert.Equal((1, 0), (uploads[1].X, uploads[1].Y));
    }

    // A chest has 10 slots: 11 items do not fit, and the answer says how many there are.
    [AvaloniaFact]
    public void AContainerRefusesMoreItemsThanSlots()
    {
        using var r = new ClaudeRun();
        var (uploads, refusal) = ClaudeTools.ContainerItems(Save.StableHash.Of("piece_chest_wood"), "chest", [.. Enumerable.Range(0, 11).Select(_ => new ClaudeTools.ItemSpec { Item = "Wood" })]);
        Assert.Null(uploads);
        Assert.Equal("chest has 10 slots, not 11.", refusal);
    }

    // A sign takes its text as a change of its own.
    [AvaloniaFact]
    public void ASignTakesItsText()
    {
        using var r = new ClaudeRun();
        var set = new List<FieldChange>();
        Assert.Null(ClaudeTools.TextAndStars(Save.StableHash.Of("sign"), "Wooden sign", "hello", null, set));
        Assert.Equal(new FieldChange("strings", "text", "hello"), Assert.Single(set));
    }

    // Text is refused for an object that is not a sign, and stars for one that is not a creature.
    [AvaloniaFact]
    public void TextAndStarsAreRefusedForOtherObjects()
    {
        using var r = new ClaudeRun();
        var set = new List<FieldChange>();
        Assert.Equal("Beech1 is not a sign: only signs take a text.", ClaudeTools.TextAndStars(Save.StableHash.Of("Beech1"), "Beech1", "hello", null, set));
        Assert.Equal("Beech1 is not a creature: only creatures have stars.", ClaudeTools.TextAndStars(Save.StableHash.Of("Beech1"), "Beech1", null, 1, set));
        Assert.Empty(set);
    }
}
