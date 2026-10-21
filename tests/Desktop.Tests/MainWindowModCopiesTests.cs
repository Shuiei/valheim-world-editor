using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a mod keeps with copies in the window (IAreaCopier): taken from the copied place, measured from the copy's
/// middle, and put down where each copy lands, in the paste's own undo step. Runs alone (the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowModCopiesTests : IDisposable
{
    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>Keeps one byte of every place copied, and pastes a fleet register (a data holder) at each copy's middle.</summary>
    private sealed class Registers : IWorldEditorExtension, IAreaCopier
    {
        public CopyRegion? Copied { get; private set; }

        public List<CopyPlacement> Landed { get; } = [];

        public void Register(IExtensionRegistry registry) => registry.AddAreaCopier("registers", this);

        public byte[]? Copy(IAreaView area, CopyRegion region)
        {
            Copied = region;
            return [3];
        }

        public ToolEdit? Paste(IAreaView area, byte[] kept, IReadOnlyList<CopyPlacement> copies)
        {
            Landed.AddRange(copies);
            var edit = new ToolEdit();
            foreach (var c in copies)
            {
                edit.Add(ExampleMod.Fleet, c.Middle, Vector3.Zero, d => d.SetInt("ships", kept[0]));
            }
            return edit;
        }
    }

    [AvaloniaFact]
    public void WhatAModKeepsGoesWithTheCopyAndIsPastedInTheSameStep()
    {
        var mod = new Registers();
        ModExtensions.UseForTests(("Fleet", mod));
        var (w, s) = TestWindows.EditingFlat();
        w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        w.View.Area.Points.Add(new Vector2(32, 32));
        w.View.Area.Points.Add(new Vector2(48, 48));

        // Bare ground: nothing but what the mod keeps, which is enough for a copy.
        w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        Assert.Equal([3], w.View.Paste.Clip!.Kept["Fleet/registers"]);
        Assert.Equal(new Vector3(8, 30, 8), mod.Copied!.Middle);
        Assert.True(mod.Copied.Contains(8, 8));
        Assert.False(mod.Copied.Contains(30, 8));

        w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        w.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, "r");
        w.PasteAt(new Vector2(80, 80));
        var landed = Assert.Single(mod.Landed);
        Assert.Equal((new Vector3(48, 30, 48), 90f, false), (landed.Middle, landed.Turn, landed.Mirror));
        var step = Assert.Single(s.UndoList);
        int id = Assert.Single(step.Hidden).Id;
        Assert.False(s.Edits.IsGone(id));
        Assert.True(s.Undo());
        Assert.True(s.Edits.IsGone(id));
        w.Close();
    }
}
