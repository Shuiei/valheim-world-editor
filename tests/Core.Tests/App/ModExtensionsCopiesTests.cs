using System.Numerics;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What running extensions keep of a copied place and put down where it is pasted (ModExtensions.CopyArea, PasteArea):
/// kept under the mod's name and the copier's id (the first of a name), put down by the copier of that name, what none
/// reads listed; a copier that throws turns its extension off. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsCopiesTests : IDisposable
{
    /// <summary>Forgets the extensions in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>A copier keeping the bytes given, its paste adding one object at each copy's middle.</summary>
    private sealed class Copier(Func<byte[]?> copy) : IAreaCopier
    {
        public List<(byte[] Kept, int Copies)> Pasted { get; } = [];

        public byte[]? Copy(IAreaView area, CopyRegion region) => copy();

        public ToolEdit? Paste(IAreaView area, byte[] kept, IReadOnlyList<CopyPlacement> copies)
        {
            Pasted.Add((kept, copies.Count));
            var edit = new ToolEdit();
            foreach (var c in copies)
            {
                edit.Add("marker", c.Middle, Vector3.Zero);
            }
            return edit;
        }
    }

    /// <summary>An extension registering copiers under the ids given.</summary>
    private sealed class Copiers(params (string Id, IAreaCopier Copier)[] copiers) : IWorldEditorExtension
    {
        public void Register(IExtensionRegistry registry)
        {
            foreach (var (id, copier) in copiers)
            {
                registry.AddAreaCopier(id, copier);
            }
        }
    }

    private static readonly CopyRegion Region = new(Vector3.Zero, [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)], (_, _) => true);

    [Fact]
    public void WhatIsKeptGoesUnderTheModsNameAndIsPutDownByItsCopier()
    {
        var first = new Copier(() => [1]);
        var again = new Copier(() => [2]);
        var empty = new Copier(() => []);
        ModExtensions.UseForTests(("Mod", new Copiers(("rock", first), ("rock", again), ("none", empty), ("bad id", new Copier(() => [3])), ("nothing", null!))));

        Assert.True(ModExtensions.AnyCopiers);
        var kept = ModExtensions.CopyArea(null!, Region);
        Assert.Equal(["Mod/rock"], kept.Keys);
        Assert.Equal([1], kept["Mod/rock"]);

        var unread = new List<string>();
        var copies = new[] { new CopyPlacement(new Vector3(5, 0, 5), 0, false), new CopyPlacement(new Vector3(9, 0, 5), 0, false) };
        var edit = Assert.Single(ModExtensions.PasteArea(null!, new Dictionary<string, byte[]>(kept) { ["Gone/rock"] = [4] }, copies, unread));
        Assert.Equal([new Vector3(5, 0, 5), new Vector3(9, 0, 5)], edit.Added.Select(a => a.Position));
        var (pastedBytes, pastedCopies) = Assert.Single(first.Pasted);
        Assert.Equal([1], pastedBytes);
        Assert.Equal(2, pastedCopies);
        Assert.Empty(again.Pasted);
        Assert.Equal(["Gone/rock"], unread);
    }

    [Fact]
    public void ACopierThatThrowsTurnsItsExtensionOff()
    {
        var fine = new Copier(() => [7]);
        ModExtensions.UseForTests(("Broken", new Copiers(("x", new Copier(() => throw new InvalidOperationException("no"))))), ("Fine", new Copiers(("x", fine))));

        var kept = ModExtensions.CopyArea(null!, Region);
        Assert.Equal(["Fine/x"], kept.Keys);
        Assert.Equal(ModExtensions.Status.Failed, ModExtensions.Offered.Single(e => e.Mod == "Broken").State);

        // Turned off, its copier reads nothing more.
        var unread = new List<string>();
        Assert.Single(ModExtensions.PasteArea(null!, new Dictionary<string, byte[]> { ["Broken/x"] = [1], ["Fine/x"] = [7] }, [new CopyPlacement(Vector3.Zero, 0, false)], unread));
        Assert.Equal(["Broken/x"], unread);
    }

    [Fact]
    public void WithoutCopiersNothingIsKept()
    {
        Assert.False(ModExtensions.AnyCopiers);
        Assert.Empty(ModExtensions.CopyArea(null!, Region));
    }
}
