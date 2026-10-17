using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class RoomReader
        {
            /// <summary>
            /// Adds one MonoBehaviour of a walked object: a ZNetView when enabled, a RandomSpawn, or what the other kinds
            /// say of their fields (only the room's openings count on its root).
            /// </summary>
            /// <param name="gid">The GameObject.</param>
            /// <param name="name">Its name.</param>
            /// <param name="kind">What the behaviour is (<c>Kind</c>).</param>
            /// <param name="pid">The behaviour's path id.</param>
            /// <param name="pos">The object's position in the prefab's frame.</param>
            /// <param name="rot">The object's rotation in the prefab's frame.</param>
            /// <param name="root">Whether the object is the room's root.</param>
            private void AddBehaviour(long gid, string name, string kind, long pid, V3 pos, Quat rot, bool root)
            {
                if (root && kind != "connection")
                {
                    return;
                }
                // A ZNetView: only whether it is enabled.
                if (kind == "netview")
                {
                    if (_open.Enabled(pid))
                    {
                        _contents.Add(new List<object?> { PrefabName(name) }.Concat(Placement(pos, rot, root)).Append(Node(gid)).ToList());
                    }
                    return;
                }
                if (kind == "spawn" && _open.Spawn(pid) is { } raw)
                {
                    if (Truthy(raw.GetValueOrDefault("m_Enabled", 1L)))
                    {
                        AddSpawn(gid, (n, fallback) => raw.TryGetValue(n, out object? v) ? v : fallback);
                    }
                    return;
                }
                if (_open.B.Field(pid) is { } b && Truthy(Get(b, "m_Enabled", 1L)))
                {
                    AddComponent(gid, kind, b, pos, rot, root);
                }
            }

            /// <summary>
            /// Adds an enabled component of a walked object by its kind: an opening, a teleport, a spawn or a pick.
            /// </summary>
            /// <param name="gid">The GameObject.</param>
            /// <param name="kind">What the component is (<c>Kind</c>).</param>
            /// <param name="b">The component.</param>
            /// <param name="pos">The object's position in the prefab's frame.</param>
            /// <param name="rot">The object's rotation in the prefab's frame.</param>
            /// <param name="root">Whether the object is the room's root.</param>
            private void AddComponent(long gid, string kind, AssetTypeValueField b, V3 pos, Quat rot, bool root)
            {
                switch (kind)
                {
                    case "connection":
                        _openings.Add(new List<object?> { Py(b["m_type"]), Int(Py(b["m_entrance"])), Int(Py(b["m_allowDoor"])), Int(Get(b, "m_doorOnlyIfOtherAlsoAllowsDoor", 0L)) }.Concat(Placement(pos, rot, root)).ToList());
                        break;
                    case "teleport":
                        // A location's door into its interior, or out of it (5000 m up in the prefab): where it stands
                        // and which way it faces, in the prefab's frame.
                        _teleports.Add(Placement(pos, rot, root));
                        break;
                    case "spawn":
                        AddSpawn(gid, (n, fallback) => n == "m_OffObject" ? b["m_OffObject.m_PathID"].AsLong : Get(b, n, fallback));
                        break;
                    case "pick":
                        AddPick(gid, b);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
