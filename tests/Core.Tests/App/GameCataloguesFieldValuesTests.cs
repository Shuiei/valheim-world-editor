using AssetsTools.NET;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;
using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests;

/// <summary>
/// The game data's values (GameCatalogues.FieldValues): a field read as a value (every width of number, text, lists,
/// structures, nothing for a missing field, raw bytes refused), what makes a piece's fields, and the rounding and comparing
/// of values: floats are rounded as Python rounds them, whole numbers and nothing are left as they are, and of two equal
/// values the first is kept.
/// </summary>
public class GameCataloguesFieldValuesTests
{
    [Fact]
    public void AFloatIsRoundedToTheDecimalsAsked() => Assert.Equal(2.46, GameCatalogues.Round(2.4567, 2));

    [Fact]
    public void AWholeNumberIsLeftAsItIs() => Assert.Equal(7L, GameCatalogues.Round(7L, 2));

    [Fact]
    public void NothingIsLeftAsNothing() => Assert.Null(GameCatalogues.Round(null, 2));

    [Fact]
    public void RoundsHalfwayToEven()
    {
        Assert.Equal(2.0, GameCatalogues.R(2.5, 0));
        Assert.Equal(4.0, GameCatalogues.R(3.5, 0));
    }

    [Fact]
    public void TheLargerOfTwoValuesIsTakenWhicheverComesFirst()
    {
        Assert.Equal(3.0, GameCatalogues.Max(1.0, 3.0));
        Assert.Equal(3.0, GameCatalogues.Max(3.0, 1.0));
    }

    [Fact]
    public void OfEqualValuesTheFirstIsKept()
    {
        Assert.True(double.IsNegative(GameCatalogues.Max(-0.0, 0.0)));
    }

    [Fact]
    public void EachKindOfNumberAndTextIsReadAsItsValue()
    {
        // Whole numbers of every width as longs, bools as bools, floats as doubles; a structure by its fields' names.
        var all = Struct("All", "m_all", Of("bool", "b", align: true), Of("SInt8", "i8"), Of("UInt8", "u8"), Of("SInt16", "i16"), Of("UInt16", "u16"),
            Of("int", "i32"), Of("UInt32", "u32"), Of("SInt64", "i64"), Of("UInt64", "u64"), Of("float", "f"), Of("double", "d"), Text("s"));
        var values = new Fields
        {
            ["m_all"] = new Fields
            {
                ["b"] = true,
                ["i8"] = -3,
                ["u8"] = 200,
                ["i16"] = -300,
                ["u16"] = 60000,
                ["i32"] = -70000,
                ["u32"] = 4000000000,
                ["i64"] = -5000000000,
                ["u64"] = 6000000000,
                ["f"] = 0.5,
                ["d"] = 0.25,
                ["s"] = "Wood",
            },
        };
        Assert.Equal("""{"b":true,"i8":-3,"u8":200,"i16":-300,"u16":60000,"i32":-70000,"u32":4000000000,"i64":-5000000000,"u64":6000000000,"f":0.5,"d":0.25,"s":"Wood"}""",
            ReadBack(values, [all], m => Json(Py(m["m_all"]))));
    }

    [Fact]
    public void AListIsReadAsItsItems()
    {
        var values = new Fields { ["m_list"] = new List<object> { 1, 2, 3 } };
        Assert.Equal("[1,2,3]", ReadBack(values, [List("m_list", Of("int", "data"))], m => Json(Py(m["m_list"]))));
    }

    [Fact]
    public void AMissingFieldIsReadAsNothing() => Assert.Null(ReadBack([], [], m => Py(m["m_none"])));

    [Fact]
    public void RawBytesAreNotAValue()
    {
        var values = new Fields { ["m_blob"] = new byte[] { 1, 2 } };
        var e = Assert.Throws<NotSupportedException>(() => ReadBack(values, [Bytes("m_blob")], m => Py(m["m_blob"])));
        Assert.Contains("m_blob", e.Message);
    }

    [Fact]
    public void APieceHasACategoryResourcesAndACraftingStation()
    {
        UnityField category = Of("int", "m_category"), resources = List("m_resources", Of("int", "data")), station = Ref("m_craftingStation");
        var values = new Fields { ["m_category"] = 2, ["m_resources"] = new List<object>(), ["m_craftingStation"] = Fields.Ref(0) };
        Assert.False(ReadBack(values, [], IsPiece));
        Assert.False(ReadBack(values, [category], IsPiece));
        Assert.False(ReadBack(values, [category, resources], IsPiece));
        Assert.True(ReadBack(values, [category, resources, station], IsPiece));
    }

    /// <summary>
    /// A made-up prefab's MonoBehaviour with these fields and values, saved and read back as the catalogue reads it.
    /// </summary>
    /// <param name="values">Its fields' values (those of fields it does not have are not written).</param>
    /// <param name="fields">Its fields after the MonoBehaviour's own.</param>
    /// <param name="read">What is read of its fields while its bundle is open.</param>
    private static T ReadBack<T>(Fields values, UnityField[] fields, Func<AssetTypeValueField, T> read)
    {
        using var game = new TestGame();
        var b = game.Bundle("values");
        long mono = 0;
        b.Prefab("thing", r => mono = r.Mono("Thing", values, fields: fields));
        game.Save();
        using var g = new GameCatalogues.Game(game.BundlesDir);
        using var bundle = g.Open("values")!;
        return read(bundle.Field(mono)!);
    }

    /// <summary>A field's value (GameCatalogues.Py).</summary>
    private static object? Py(AssetTypeValueField f) => PrivateMember.Call(typeof(GameCatalogues), "Py", null, f);

    /// <summary>Whether the fields are a piece's (GameCatalogues.IsPiece).</summary>
    private static bool IsPiece(AssetTypeValueField f) => PrivateMember.Call<bool>(typeof(GameCatalogues), "IsPiece", null, f);

    /// <summary>A value as compact JSON.</summary>
    private static string Json(object? v) => CatalogueJson.Write(v, new CatalogueJson.Style(",", ":"));
}
