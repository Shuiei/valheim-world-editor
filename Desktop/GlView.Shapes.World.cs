using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The world round a thing as an extension's shapes see it: the things by the ids of the linked objects.
public sealed partial class GlView
{
    /// <summary>
    /// The world around a thing as an extension's shapes see it (IWorldView): the area's things by the ids linked
    /// objects hold (LinkIndex), each read once.
    /// </summary>
    private sealed class ShapeWorld(Save.WorldSave world, EditSession session, List<WorldScene.Thing> things) : ValheimWorldEditor.Sdk.IWorldView
    {
        /// <summary>The linked objects, read when first needed.</summary>
        private LinkIndex? _links;

        /// <summary>The things not gone, by object id (the first when two have it), made when first needed.</summary>
        private Dictionary<int, WorldScene.Thing>? _byId;

        /// <summary>The views made, by object id.</summary>
        private readonly Dictionary<int, ObjectView?> _views = [];

        /// <summary>A thing as an extension sees it, from its data; null when its data cannot be read.</summary>
        public static ObjectView? ViewOf(Save.WorldSave world, EditSession session, WorldScene.Thing t) =>
            ObjectData.Bytes(world, session.Edits, t.Id) is { } bytes
                ? new ObjectView(Terrain.PrefabCatalog.NameOf(t.Prefab) ?? "", t.Position, t.Rotation, Save.ZdoData.Parse(bytes), t.Scale)
                : null;

        /// <inheritdoc/>
        public bool TryFind(string idKey, int id, out ValheimWorldEditor.Sdk.IObjectView obj)
        {
            obj = null!;
            _links ??= session.ReadLinks();
            if (_links.Holder(Save.StableHash.Of(idKey), id) is not { } holder)
            {
                return false;
            }
            if (_byId == null)
            {
                _byId = [];
                foreach (var t in things)
                {
                    _byId.TryAdd(t.Id, t);
                }
            }
            if (!_byId.TryGetValue(holder.Id, out var thing))
            {
                return false;
            }
            if (!_views.TryGetValue(thing.Id, out var view))
            {
                _views[thing.Id] = view = ViewOf(world, session, thing);
            }
            obj = view!;
            return view != null;
        }
    }
}
