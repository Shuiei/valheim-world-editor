using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>What the pointer did over the 3D view (<see cref="IEditorTool.OnPointer"/>).</summary>
public enum PointerAction
{
    /// <summary>Moved, no button held.</summary>
    Move,

    /// <summary>The left button pressed.</summary>
    Press,

    /// <summary>Moved with the left button held.</summary>
    Drag,

    /// <summary>The left button let go.</summary>
    Release,
}

/// <summary>What the pointer is over.</summary>
public enum PointerTarget
{
    /// <summary>Nothing (the sky, outside the area).</summary>
    None,

    /// <summary>The editor's ground.</summary>
    Ground,

    /// <summary>A mesh of an extension's scene layer (<see cref="SceneMesh"/>).</summary>
    Scene,

    /// <summary>An object or building piece (<see cref="PointerInput.Thing"/>).</summary>
    Thing,
}

/// <summary>The pointer over the 3D view, in the world's frame (metres; x east, y up, z north).</summary>
public sealed class PointerInput
{
    /// <summary>The pointer's state.</summary>
    /// <param name="action">What it did.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <param name="origin">Where its ray starts (the camera's near plane).</param>
    /// <param name="direction">The ray's direction (unit length).</param>
    public PointerInput(PointerAction action, KeyModifiers modifiers, Vector3 origin, Vector3 direction)
    {
        Action = action;
        Modifiers = modifiers;
        Origin = origin;
        Direction = direction;
    }

    /// <summary>What it did.</summary>
    public PointerAction Action { get; }

    /// <summary>The modifier keys held.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>Where the ray through the pointer starts.</summary>
    public Vector3 Origin { get; }

    /// <summary>The ray's direction.</summary>
    public Vector3 Direction { get; }

    /// <summary>What the ray meets first.</summary>
    public PointerTarget Target { get; set; }

    /// <summary>Where it meets it (when <see cref="Target"/> is not None).</summary>
    public Vector3 Point { get; set; }

    /// <summary>The surface's normal there (towards the ray's side; up for the ground's crossing).</summary>
    public Vector3 Normal { get; set; }

    /// <summary>The object met (for <see cref="PointerTarget.Thing"/>).</summary>
    public IObjectView? Thing { get; set; }
}
