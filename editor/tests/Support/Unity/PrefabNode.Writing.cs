namespace WorldEditor.Tests.Unity;

/// <summary>
/// Adding a node's children and writing the node's GameObject and Transform into the bundle.
/// </summary>
public sealed partial class PrefabNode
{
    /// <summary>
    /// Adds a child (built by <paramref name="build"/>); returns it.
    /// </summary>
    public PrefabNode Child(string name, Action<PrefabNode>? build = null)
    {
        var child = new PrefabNode(_b, name, Tr);
        build?.Invoke(child);
        child.Write();
        _children.Add(child.Tr);
        return child;
    }

    /// <summary>
    /// Writes the GameObject (Transform first among its components) and the Transform.
    /// </summary>
    internal void Write()
    {
        _b.Assets.Add(UnityClass.GameObject, UnityTypes.GameObject, new Fields
        {
            ["m_Component"] = new[] { Tr }.Concat(_components).Select(c => new Fields { ["component"] = Fields.Ref(c) }).ToList(),
            ["m_Layer"] = Layer,
            ["m_Name"] = Name,
            ["m_Tag"] = Tag,
            ["m_IsActive"] = Active,
        }, pathId: Go);
        _b.Assets.Add(UnityClass.Transform, UnityTypes.Transform, new Fields
        {
            ["m_GameObject"] = Fields.Ref(Go),
            ["m_LocalRotation"] = Fields.Quaternion(Rotation.X, Rotation.Y, Rotation.Z, Rotation.W),
            ["m_LocalPosition"] = Fields.Vector3(Position.X, Position.Y, Position.Z),
            ["m_LocalScale"] = Fields.Vector3(Scale.X, Scale.Y, Scale.Z),
            ["m_Children"] = _children.Select(c => Fields.Ref(c)).ToList(),
            ["m_Father"] = Fields.Ref(_father),
        }, pathId: Tr);
    }
}
