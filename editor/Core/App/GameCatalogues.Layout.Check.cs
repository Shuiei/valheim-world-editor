namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    internal static partial class Layout
    {
        /// <summary>
        /// The layout read as the type tree says, for one GameObject: whether it is a root agrees with
        /// Roots. A game made with another Unity could lay objects out otherwise: said, rather than read
        /// wrong. Throws InvalidDataException when it does not.
        /// </summary>
        public static void Check(Bundle b, long go, HashSet<long> roots)
        {
            if (b.Field(go) is not { } g || b.ComponentIds(g).FirstOrDefault(c => c.Type == TransformType) is not (_, long tr and not 0) || b.Field(tr) is not { } t)
            {
                return;
            }
            if ((t["m_Father.m_PathID"].AsLong == 0) != roots.Contains(go))
            {
                throw new InvalidDataException("The game's objects are not laid out as the editor expects (another Unity version?).");
            }
        }

        /// <summary>
        /// The tree as the type tree says, for one GameObject and its Transform: said, rather than read
        /// wrong (as Check above). Throws InvalidDataException when they differ.
        /// </summary>
        public static void Check(Bundle b, Bundle.Inner f, long go, Tree tree)
        {
            if (b.Field(f, go) is not { } g || !tree.GameObjects.TryGetValue((f, go), out var raw))
            {
                return;
            }
            var comps = raw.Components;
            bool same = g["m_Name"].AsString == raw.Name && g["m_Component.Array"].Children.Select(c => (c["component.m_FileID"].AsInt, c["component.m_PathID"].AsLong)).SequenceEqual(comps);
            if (comps.FirstOrDefault(c => Bundle.TypeOf(f, c.Pid) == TransformType) is (_, long tr and not 0) && b.Field(f, tr) is { } t && tree.Transforms.TryGetValue((f, tr), out var node))
            {
                same &= t["m_GameObject.m_PathID"].AsLong == node.Go && (t["m_Father.m_FileID"].AsInt == 0 && t["m_Father.m_PathID"].AsLong == 0) == node.Root
                    && t["m_Children.Array"].Children.Select(c => c["m_PathID"].AsLong).SequenceEqual(node.Children);
            }
            if (!same)
            {
                throw new InvalidDataException("The game's objects are not laid out as the editor expects (another Unity version?).");
            }
        }
    }
}
