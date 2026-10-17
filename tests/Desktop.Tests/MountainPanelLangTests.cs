using System.Numerics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Mountain tool in another language (the made-up one: PseudoLang): with each preset chosen (its help line, the
/// Turn row of ridges and ranges) its panel shows only catalogue texts, while the presets keep the English names
/// scripts give; and every message it gives (too big, placed, what it buried, growing, the area changing meanwhile)
/// and its History steps are in that language, while Claude reads the messages in English.
/// </summary>
[Collection("DataDir")]
public sealed class MountainPanelLangTests
{
    [AvaloniaFact]
    public void MountainPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var p = new MountainPanel();
        for (int i = 0; i < Mountain.Presets.Length; i++)
        {
            p.PresetBox.SelectedIndex = i;
            Assert.Empty(PseudoLang.Untranslated(p.Card));
            Assert.StartsWith(PseudoLang.Open.ToString(), p.HelpText.Text, StringComparison.Ordinal);
        }
        // Scripts name a preset in English, whatever language the window shows.
        Assert.Equal("Lone peak", Mountain.Presets[0].Name);
        Assert.Equal(PseudoLang.Wrap("Lone peak"), Mountain.Presets[0].Shown);
    }

    [AvaloniaFact]
    public async Task EveryKindsMessagesAreSaidInTheLanguageAndClaudeReadsEnglish()
    {
        using var pseudo = new PseudoLang();
        // A tree in the middle, where the mountains go: the lone peak buries it.
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false);
        var (w, s) = TestWindows.Editing(tree);
        var m = w.MountainPanel;
        m.GrowBox.IsChecked = false;
        for (int i = 0; i < Mountain.Presets.Length; i++)
        {
            var preset = Mountain.Presets[i];
            string name = Regex.Escape(preset.Name.ToLowerInvariant());
            m.PresetBox.SelectedIndex = i;
            m.RadiusSlider.Value = 30;
            // Near the edge: it does not fit.
            await w.PutMountain(5, 5);
            Assert.Empty(PseudoLang.Untranslated(w.MessageText));
            Assert.Matches($@"^The {name} reaches \d+ m around the click and does not fit in the open area: ", w.LastMessageEn);
            // In the middle: placed, with what it buried, and one step to take back.
            await w.PutMountain(64, 64);
            Assert.Empty(PseudoLang.Untranslated(w.MessageText));
            Assert.Matches($@"^Placed a {name}, \d+ m high(; \d+ tree\(s\) and rock\(s\) it buried taken away)?\. Ctrl\+Z takes it back; saving turns it into ground discs\.$", w.LastMessageEn);
            if (i == 0)
            {
                Assert.Contains("; 1 tree(s) and rock(s) it buried taken away.", w.LastMessageEn, StringComparison.Ordinal);
            }
            Assert.Equal(PseudoLang.Wrap("Mountain: {0}").Replace("{0}", preset.Shown, StringComparison.Ordinal), s.UndoLabel);
            Assert.True(s.Undo());
        }
        // Growing where there is no world generator: why nothing grew stays said, in the language.
        m.PresetBox.SelectedIndex = 0;
        m.RadiusSlider.Value = 30;
        m.GrowBox.IsChecked = true;
        await w.PutMountain(64, 64);
        Assert.Empty(PseudoLang.Untranslated(w.MessageText));
        Assert.Contains(" Regrow needs the world's generator", w.LastMessageEn, StringComparison.Ordinal);
        Assert.EndsWith(" Ctrl+Z takes it back; saving turns it into ground discs.", w.LastMessageEn, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task GrowingOnAMountainIsSaidInTheLanguageAndClaudeReadsEnglish()
    {
        using var pseudo = new PseudoLang();
        using var run = new WorldWindowRun();
        await run.Open(size: 3);
        var w = run.W;
        var s = w.Session!;
        w.MountainPanel.RadiusSlider.Value = 25;
        w.MountainPanel.GrowBox.IsChecked = true;
        float gx = (s.Ground.W - 1) / 2f, gz = (s.Ground.H - 1) / 2f;
        int steps = s.UndoList.Count;

        // What grew: said in the language, its History step too.
        await w.PutMountain(gx, gz);
        Assert.Empty(PseudoLang.Untranslated(w.MessageText));
        var grew = Regex.Match(w.LastMessageEn, @" Grew (\d+) of the biome's trees and rocks on it\. Ctrl\+Z takes it back \(twice with what grew\); saving turns it into ground discs\.$");
        Assert.True(grew.Success, w.LastMessageEn);
        Assert.Equal(steps + 2, s.UndoList.Count);
        Assert.Equal(PseudoLang.Wrap("Mountain: grew {0} object(s)").Replace("{0}", grew.Groups[1].Value, StringComparison.Ordinal), s.UndoLabel);
        Assert.True(s.Undo());
        Assert.True(s.Undo());

        // Ctrl+Z while the trees are worked out: nothing grows, and that is said in the language.
        bool undone = false;
        w.MessageText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty && !undone && w.LastMessageEn.EndsWith("Working out what the game grows here…", StringComparison.Ordinal))
            {
                undone = true;
                s.Undo();
            }
        };
        await w.PutMountain(gx, gz);
        Assert.True(undone);
        Assert.Empty(PseudoLang.Untranslated(w.MessageText));
        Assert.EndsWith(" Nothing grew on it: the area changed while its trees were worked out.", w.LastMessageEn, StringComparison.Ordinal);
        Assert.Equal(steps, s.UndoList.Count);
    }
}
