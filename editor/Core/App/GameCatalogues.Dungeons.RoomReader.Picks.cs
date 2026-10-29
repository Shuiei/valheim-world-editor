using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class RoomReader
        {
            /// <summary>
            /// Adds a RandomObject (pick): its node, required theme, the choices below it with their weights, and its
            /// elevations.
            /// </summary>
            /// <param name="gid">The GameObject the pick is on.</param>
            /// <param name="b">The pick's component.</param>
            private void AddPick(long gid, AssetTypeValueField b)
            {
                var choices = new List<object?>();
                foreach (var e in b["m_objects.Array"].Children)
                {
                    long cid = e["m_object.m_PathID"].AsLong;
                    if (cid != 0 && Below(cid))
                    {
                        choices.Add(new List<object?> { Node(cid), Round(Py(e["m_weight"]), 4) });
                    }
                }
                _picks.Add(new List<object?> { Node(gid), Get(b, "m_dungeonRequireTheme", 0L), choices, Get(b, "m_minElevation", -10000L), Get(b, "m_maxElevation", 10000L) });
            }
        }
    }
}
