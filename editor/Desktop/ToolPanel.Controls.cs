using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>The Select tool's rail button.</summary>
    internal Button SelectButton { get; }
    /// <summary>Blueprints: the library, to paste one (in a world), in place of a tool's options.</summary>
    internal Button BlueprintsButton { get; }
    /// <summary>The options card's title: the brush's name (Paint for every paint).</summary>
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
    /// <summary>The options card's help line for the brush.</summary>
    private readonly TextBlock _help = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The option rows shown only for some brushes or shapes (Paint, Flatten, Naturalize, Turn, Erode, Falloff).</summary>
    private readonly Control _paintRows, _flattenRows, _naturalRows, _turnRow, _erodeRows, _falloffRow;
    /// <summary>
    /// The stamps in the Shape list after the four shapes: the built-in ones, then loaded pictures. The Landform
    /// tool's Picture kind offers the same pictures (<c>StampsChanged</c> tells it when one is loaded or forgotten).
    /// </summary>
    internal List<Stamps.Stamp> StampList { get; } = [.. Stamps.BuiltIn, .. Stamps.LoadKept()];
    /// <summary>A picture was loaded or forgotten: <c>StampList</c> changed.</summary>
    public event Action? StampsChanged;
    /// <summary>Load stamp…: asks the window for a picture (<c>LoadStampAsked</c>).</summary>
    internal Button LoadStampButton { get; } = new() { Content = Lang.T("brush.label.loadStamp"), FontSize = 12 };
    /// <summary>Forget stamp: drops the loaded picture chosen as Shape (built-in stamps stay).</summary>
    internal Button ForgetStampButton { get; } = new() { Content = Lang.T("brush.label.forgetStamp"), FontSize = 12 };
    /// <summary>Load stamp…: the window picks the picture (and calls <c>AddStamp</c>).</summary>
    public event Action? LoadStampAsked;
    /// <summary>A line for the window's message bar, in both languages.</summary>
    public event Action<Said>? Message;
    /// <summary>Erode's Thermal mode: slopes steeper than the rest angle slide down.</summary>
    internal Button ThermalButton { get; } = new() { Content = Lang.T("brush.label.thermal"), FontSize = 12 };
    /// <summary>Erode's Water mode (the rest angle does not apply).</summary>
    internal Button WaterButton { get; } = new() { Content = Lang.T("brush.label.water"), FontSize = 12 };
    /// <summary>Thermal erosion's rest angle, in degrees (10..60).</summary>
    internal Slider RestSlider { get; }

    /// <summary>The brush radius in metres (1..30). For tests too.</summary>
    internal Slider SizeSlider { get; }
    /// <summary>Naturalize: a new noise pattern for the next stroke (and natural paths).</summary>
    internal Button NewPatternButton { get; } = new() { Content = Lang.T("brush.label.newPattern"), FontSize = 12 };
    /// <summary>The brush strength (0.05..1).</summary>
    internal Slider StrengthSlider { get; }
    /// <summary>The brush shape: the four shapes, then the stamps.</summary>
    internal ComboBox ShapeBox { get; }
    /// <summary>The brush falloff, in Falloff's order.</summary>
    internal ComboBox FalloffBox { get; }
    /// <summary>Flatten: level to the height where the stroke starts, not the typed height.</summary>
    internal CheckBox TargetFromClickBox { get; }
    /// <summary>Flatten's height in metres (off while it comes from the stroke's start).</summary>
    internal NumericUpDown TargetBox { get; }
}
