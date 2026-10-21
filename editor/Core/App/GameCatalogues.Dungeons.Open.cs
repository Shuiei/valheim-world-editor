using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// The bundle being read: what each of its scripts is (once per script), and the rooms' trees read
        /// raw (Layout.Sweep): each GameObject's name, components and m_IsActive, each Transform's
        /// GameObject, children and local position and rotation, each ZNetView's m_Enabled. By path id
        /// over the bundle, as Bundle.Field(pid) finds them.
        /// </summary>
        private sealed partial class Open(Bundle b)
        {
            /// <summary>
            /// The bundle.
            /// </summary>
            public Bundle B { get; } = b;

            /// <summary>
            /// What each script is (<c>Kind</c>), by its file and script index.
            /// </summary>
            private readonly Dictionary<(int File, ushort Script), string?> _kinds = [];
            /// <summary>
            /// The GameObjects read raw: name, component path ids, m_IsActive.
            /// </summary>
            private readonly Dictionary<long, (string Name, long[] Components, bool Active)> _gos = [];
            /// <summary>
            /// The Transforms read raw: GameObject, children (Transform path ids), local position and rotation.
            /// </summary>
            private readonly Dictionary<long, (long Go, long[] Children, V3 Pos, Quat Rot)> _transforms = [];
            /// <summary>
            /// Each ZNetView's m_Enabled, read raw.
            /// </summary>
            private readonly Dictionary<long, bool> _enabled = [];
            /// <summary>
            /// <c>Node</c>'s answers, by GameObject.
            /// </summary>
            private readonly Dictionary<long, (string Name, bool Active, long Tr, List<(string Kind, long Pid)> Behaviours)> _nodes = [];
            /// <summary>
            /// <c>Transform</c>'s answers, by Transform.
            /// </summary>
            private readonly Dictionary<long, (V3 Pos, Quat Rot, List<long> Children)> _frames = [];
            /// <summary>
            /// Each GameObject's parent, of those read.
            /// </summary>
            private readonly Dictionary<long, long> _parents = [];

            /// <summary>
            /// A GameObject's parent GameObject, false when it was not read.
            /// </summary>
            public bool ParentOf(long go, out long parent) => _parents.TryGetValue(go, out parent);

            /// <summary>
            /// A MonoBehaviour's script: its file (by place) and its index there.
            /// </summary>
            private (int File, ushort Script) ScriptOf(Bundle.Inner f, AssetFileInfo info) => (B.Files.FindIndex(x => ReferenceEquals(x, f)), info.GetScriptIndex(f.Assets.file));

            /// <summary>
            /// What a MonoBehaviour is to the scan (by its script's fields, told once per script); null: not a
            /// MonoBehaviour or none that matters.
            /// </summary>
            private string? KindOf(long pid)
            {
                if (B.FileWith(pid) is not { } f || f.Assets.file.GetAssetInfo(pid) is not { TypeId: MonoBehaviourType } info)
                {
                    return null;
                }
                var key = ScriptOf(f, info);
                if (!_kinds.TryGetValue(key, out string? k))
                {
                    _kinds[key] = k = Kind(B.FieldNames(f, info));
                }
                return k;
            }

            /// <summary>
            /// A RandomSpawn's fields read raw (the many in the rooms), or null when it was read whole.
            /// </summary>
            public Dictionary<string, object?>? Spawn(long pid) => _spawns.GetValueOrDefault(pid);

            /// <summary>
            /// RandomSpawns read raw, by path id.
            /// </summary>
            private readonly Dictionary<long, Dictionary<string, object?>> _spawns = [];
            /// <summary>
            /// <c>Plan</c>'s answers, by script.
            /// </summary>
            private readonly Dictionary<(int File, ushort Script), (AssetTypeTemplateField Tree, int Last)?> _plans = [];

            /// <summary>
            /// The RandomSpawn fields the scan needs.
            /// </summary>
            private static readonly string[] SpawnFields = ["m_Enabled", "m_chanceToSpawn", "m_dungeonRequireTheme", "m_OffObject", "m_minElevation", "m_maxElevation"];

            /// <summary>
            /// How a RandomSpawn's script is read raw: its type tree and its last field that matters; null
            /// when one of its fields before that is not of a kind read here.
            /// </summary>
            private (AssetTypeTemplateField Tree, int Last)? Plan(long pid)
            {
                var f = B.FileWith(pid)!;
                var info = f.Assets.file.GetAssetInfo(pid)!;
                var key = ScriptOf(f, info);
                if (!_plans.TryGetValue(key, out var plan))
                {
                    var tree = B.Template(f, info);
                    int last = tree == null ? -1 : tree.Children.FindLastIndex(c => SpawnFields.Contains(c.Name));
                    _plans[key] = plan = last >= 0 && tree!.Children.Take(last + 1).All(c => Raw.CanSkip(c) && (!SpawnFields.Contains(c.Name) || Raw.CanRead(c))) ? (tree, last) : null;
                }
                return plan;
            }

            /// <summary>
            /// Whether a ZNetView is enabled (read raw: nothing else of it is needed).
            /// </summary>
            public bool Enabled(long netview) => _enabled.TryGetValue(netview, out bool on) && on;
        }
    }
}
