using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The walk over a prefab's objects that gives its model: its state, and the walk that places each renderer's submeshes.
/// </summary>
public sealed partial class GameBundles
{
    internal sealed partial class ModelWalk
    {
        /// <summary>
        /// The reader of the game's bundles, for the objects' fields and the references between them.
        /// </summary>
        private readonly GameBundles _owner;
        /// <summary>
        /// The bundle the prefab is in.
        /// </summary>
        private readonly string _bundle;
        /// <summary>
        /// The objects left out of the model (worn and broken parts, unspawned ones, and a room's networked objects).
        /// </summary>
        private readonly HashSet<long> _skipGo = [];
        /// <summary>
        /// The renderers left out for a lower level of detail.
        /// </summary>
        private readonly HashSet<long> _lodSkip = [];

        /// <summary>
        /// A walk over one prefab's objects in its bundle.
        /// </summary>
        /// <param name="owner">The reader of the game's bundles.</param>
        /// <param name="bundle">The bundle the prefab is in.</param>
        public ModelWalk(GameBundles owner, string bundle)
        {
            _owner = owner;
            _bundle = bundle;
        }

        /// <summary>
        /// The parts found, in the walk's order.
        /// </summary>
        public List<Part> Parts { get; } = [];

        private IEnumerable<long> ChildObjects(AssetTypeValueField tr)
        {
            foreach (var ch in tr["m_Children.Array"].Children)
            {
                if (_owner.Read(_bundle, ch["m_PathID"].AsLong) is { } co)
                {
                    yield return co["m_GameObject.m_PathID"].AsLong;
                }
            }
        }

        internal void Walk(long goPid, float[] parent, bool isRoot)
        {
            if (!isRoot && _skipGo.Contains(goPid) || _owner.Read(_bundle, goPid) is not { } go || !isRoot && !go["m_IsActive"].AsBool)
            {
                return;
            }
            var cs = _owner.Components(_bundle, go).ToList();
            var tr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F;
            if (tr == null)
            {
                return;
            }
            float[] w = isRoot ? Matrix.Identity() : Matrix.Mul(parent, Matrix.Trs(tr["m_LocalPosition"], tr["m_LocalRotation"], tr["m_LocalScale"]));
            AddRendererParts(cs, w);
            foreach (long c in ChildObjects(tr))
            {
                Walk(c, w, false);
            }
        }
    }
}
