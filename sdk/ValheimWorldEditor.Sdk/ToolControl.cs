using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>A control of a tool's card. Its value is read through <see cref="IToolContext"/> by its id.</summary>
public abstract class ToolControl
{
    /// <summary>A control.</summary>
    /// <param name="id">Its id, unique within the tool.</param>
    /// <param name="label">What it is called on the card.</param>
    protected ToolControl(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>Its id.</summary>
    public string Id { get; }

    /// <summary>What it is called on the card.</summary>
    public string Label { get; }

    /// <summary>What it does, shown when pointing at it (null: nothing).</summary>
    public string? Help { get; set; }
}

/// <summary>A number chosen on a slider (<see cref="IToolContext.Number"/>).</summary>
public sealed class SliderControl : ToolControl
{
    /// <summary>A slider.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value.</param>
    /// <param name="value">Its first value.</param>
    public SliderControl(string id, string label, float min, float max, float value)
        : base(id, label)
    {
        Min = min;
        Max = max;
        Value = value;
    }

    /// <summary>The lowest value.</summary>
    public float Min { get; }

    /// <summary>The highest value.</summary>
    public float Max { get; }

    /// <summary>Its first value.</summary>
    public float Value { get; }

    /// <summary>The steps it moves by (0: any value).</summary>
    public float Step { get; set; }

    /// <summary>The unit shown after the value ("m"), or null.</summary>
    public string? Unit { get; set; }
}

/// <summary>One of a few choices (<see cref="IToolContext.Choice"/>).</summary>
public sealed class ChoiceControl : ToolControl
{
    /// <summary>A choice.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="options">The choices, as shown; the first is chosen first.</param>
    public ChoiceControl(string id, string label, IReadOnlyList<string> options)
        : base(id, label) => Options = options;

    /// <summary>The choices, as shown.</summary>
    public IReadOnlyList<string> Options { get; }
}

/// <summary>A yes or no (<see cref="IToolContext.IsOn"/>).</summary>
public sealed class ToggleControl : ToolControl
{
    /// <summary>A toggle.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What it is called.</param>
    /// <param name="value">Its first value.</param>
    public ToggleControl(string id, string label, bool value)
        : base(id, label) => Value = value;

    /// <summary>Its first value.</summary>
    public bool Value { get; }
}

/// <summary>A button: <see cref="IEditorTool.OnControl"/> is called when it is pressed.</summary>
public sealed class ButtonControl : ToolControl
{
    /// <summary>A button.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="label">What is written on it.</param>
    public ButtonControl(string id, string label)
        : base(id, label)
    {
    }
}

/// <summary>A line of text on the card, which the tool changes (<see cref="IToolContext.SetText"/>).</summary>
public sealed class TextControl : ToolControl
{
    /// <summary>A line of text.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="text">What it says first.</param>
    public TextControl(string id, string text)
        : base(id, text)
    {
    }
}
