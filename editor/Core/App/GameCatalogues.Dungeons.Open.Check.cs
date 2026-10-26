namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class Open
        {
            /// <summary>
            /// The raw reading as the type tree says, for one GameObject and its Transform: said, rather
            /// than read wrong (as Layout.Check). Throws InvalidDataException when they differ.
            /// </summary>
            private void Check(long go)
            {
                if (!_gos.TryGetValue(go, out var raw) || B.Field(go) is not { } g)
                {
                    return;
                }
                bool same = g["m_Name"].AsString == raw.Name && Truthy(Get(g, "m_IsActive", true)) == raw.Active
                    && g["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong).SequenceEqual(raw.Components);
                if (Node(go) is { Tr: long tr and not 0 } && _transforms.TryGetValue(tr, out var rt) && B.Field(tr) is { } t)
                {
                    var rot = Quat.Of(t["m_LocalRotation"]);
                    same &= t["m_GameObject.m_PathID"].AsLong == rt.Go && t["m_Children.Array"].Children.Select(c => c["m_PathID"].AsLong).SequenceEqual(rt.Children)
                        && Vec(t["m_LocalPosition"]) == rt.Pos && rot == rt.Rot;
                }
                if (!same)
                {
                    throw Unexpected();
                }
            }

            /// <summary>
            /// The error for objects not laid out as the raw reading expects.
            /// </summary>
            private static InvalidDataException Unexpected() => new("The game's objects are not laid out as the editor expects (another Unity version?).");
        }
    }
}
