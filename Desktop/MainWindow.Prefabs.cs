using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A prefab's name for people (its model's, else the catalogue's); null when unknown.
    /// </summary>
    internal string? NameOfPrefab(int prefab) => _models?.NameOf(prefab) ?? Terrain.PrefabCatalog.DisplayName(prefab);

    /// <summary>
    /// Model boxes by kind name, worked out once (null: no model).
    /// </summary>
    private readonly Dictionary<string, (System.Numerics.Vector3, System.Numerics.Vector3)?> _boxes = [];

    /// <summary>
    /// A kind's model box in its own frame, in Unity's axes (the models are stored with z mirrored) and scaled like the
    /// model; null when it has no model (or the game's models are not open).
    /// </summary>
    private (System.Numerics.Vector3 Min, System.Numerics.Vector3 Max)? ModelBox(string name)
    {
        if (_boxes.TryGetValue(name, out var known))
        {
            return known;
        }
        if (_models?.LoadModel(name) is not { } model)
        {
            return _boxes[name] = null;
        }
        System.Numerics.Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
        foreach (var part in model.Parts)
        {
            if (_models.LoadMesh(part.Mesh) is { } md)
            {
                var (a, b) = Picking.Transform(md.Bounds.Min, md.Bounds.Max, part.Matrix);
                lo = System.Numerics.Vector3.Min(lo, a);
                hi = System.Numerics.Vector3.Max(hi, b);
            }
        }
        if (lo.X > hi.X)
        {
            return _boxes[name] = null;
        }
        var sc = model.RootScale;
        return _boxes[name] = (new System.Numerics.Vector3(lo.X * sc.X, lo.Y * sc.Y, -hi.Z * sc.Z), new System.Numerics.Vector3(hi.X * sc.X, hi.Y * sc.Y, -lo.Z * sc.Z));
    }

    /// <summary>
    /// Ctrl+C: the Area selection (ground and shown objects), or the Select tool's objects.
    /// </summary>
    internal void Copy()
    {
        if (CopyToClipboard() is { } clip)
        {
            Msg(Tools.Mode == ToolMode.Select ? Said.Of("paste.msg.copiedObjects", clip.Objects.Count)
                : Said.Of("paste.msg.copiedArea", clip.W, clip.H, clip.Objects.Count));
        }
    }
}
