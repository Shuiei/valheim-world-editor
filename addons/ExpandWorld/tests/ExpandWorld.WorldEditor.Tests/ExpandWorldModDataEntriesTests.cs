using TerrainEditor.Save;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Data's data entries as the editor writes them: by name, as the shorthand or written in place, each
/// value of its list's type; and what cannot be written ahead of the game (choices, ranges, functions, animations,
/// component fields, inventories, unknown names).
/// </summary>
public class ExpandWorldModDataEntriesTests
{
    /// <summary>The data files' entries the tests resolve against.</summary>
    private static readonly ExpandWorldMod.DataTable Table = new(
    [
        E(("name", "plain"), ("ints", new List<object?> { "count, 3", "12345, 7" }), ("floats", "health, 2.5"), ("strings", new List<object?> { "label, two words", "quoted, \"a, b\"" }),
          ("longs", "owner, 9000000000"), ("bools", "on, true"), ("hashes", "item, Wood"), ("vecs", "spot, 1, 2, 3"), ("quats", "turn, 90"), ("bytes", "blob, AQID")),
        E(("name", "choice"), ("ints", "count, 1, 2")),
        E(("name", "range"), ("ints", "count, 1;5")),
        E(("name", "function"), ("strings", "label, <x>")),
        E(("name", "animation"), ("ints", "$anim_speed, 1")),
        E(("name", "field"), ("ints", "Piece.m_comfort, 1")),
        E(("name", "inventory"), ("items", new List<object?>())),
        E(("name", "group"), ("value", "Wood")),
    ]);

    /// <summary>The values of a reference, as "section key value" lines.</summary>
    private static List<string> Lines(object? reference)
    {
        var values = Table.Resolve(reference);
        Assert.Null(values.Why);
        return [.. values.Fields.Select(f => $"{f.Section} {f.Key} {f.Value}")];
    }

    [Fact]
    public void ANamedEntryGivesEachValueInItsListsType()
    {
        Assert.Equal(
        [
            $"ints {StableHash.Of("count")} 3", "ints 12345 7", $"floats {StableHash.Of("health")} 2.5",
            $"strings {StableHash.Of("label")} two words", $"strings {StableHash.Of("quoted")} a, b", $"longs {StableHash.Of("owner")} 9000000000",
            $"ints {StableHash.Of("on")} 1", $"ints {StableHash.Of("item")} {StableHash.Of("Wood")}",
            // x, z, y as the mod writes a vector; the turn about y.
            $"vec3 {StableHash.Of("spot")} 1 3 2", $"quats {StableHash.Of("turn")} 0 0.70710677 0 0.70710677", $"bytes {StableHash.Of("blob")} AQID",
        ], Lines("plain"));
    }

    [Fact]
    public void TheShorthandAndAnEntryInPlaceAreReadToo()
    {
        Assert.Equal([$"ints {StableHash.Of("count")} 4"], Lines("int, count, 4"));
        Assert.Equal([$"floats {StableHash.Of("size")} 1.5"], Lines(new Dictionary<string, object?> { ["floats"] = "size, 1.5" }));
        Assert.Empty(Lines(null));
        Assert.Empty(Lines(""));
        Assert.Empty(Lines("group"));
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("range")]
    [InlineData("function")]
    [InlineData("animation")]
    [InlineData("field")]
    [InlineData("inventory")]
    [InlineData("nowhere")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void WhatTheGameWorksOutWhenItSpawnsCannotBeWritten(string name) =>
        Assert.NotNull(Table.Resolve(name).Why);
}
