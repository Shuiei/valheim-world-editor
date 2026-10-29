using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The components of some game scripts (told by their class) in the game's prefabs, with every field the game saves
    /// but Unity's own, for mods' extensions (the manifests' "gameComponents", ModComponents): each with its prefab (the
    /// root object's name) and the object it is on.
    /// </summary>
    private sealed class ComponentScan(IReadOnlyCollection<string> classes) : IStartScan
    {
        /// <summary>Unity's fields of every MonoBehaviour, left out.</summary>
        private static readonly HashSet<string> UnityFields = ["m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier"];

        private readonly HashSet<string> _classes = [.. classes];

        /// <summary>The components found, by class.</summary>
        private readonly Dict _found = new();

        /// <summary>The components by class: a list of {"prefab", "object", "fields"} each.</summary>
        public string Json() => CatalogueJson.Write(_found, new CatalogueJson.Style(",", Indent: 0));

        /// <summary>Reads the components of the classes in one bundle.</summary>
        public void Read(Bundle b, Game game)
        {
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId != MonoBehaviourType || game.ScriptClass(b, f, info) is not { } cls || !_classes.Contains(cls)
                    || b.Field(f, info.PathId) is not { } m || b.Field(m["m_GameObject.m_PathID"].AsLong) is not { } go
                    || Chain.Up(b, m["m_GameObject.m_PathID"].AsLong) is not var (root, _, _, _) || b.Field(root) is not { } top)
                {
                    continue;
                }
                var fields = new Dict();
                foreach (var c in m.Children.Where(c => !UnityFields.Contains(c.TemplateField.Name)))
                {
                    fields[c.TemplateField.Name] = Py(c);
                }
                var list = _found.TryGetValue(cls, out object? l) ? (List<object?>)l! : (List<object?>)(_found[cls] = new List<object?>())!;
                list.Add(new Dict { ["prefab"] = top["m_Name"].AsString, ["object"] = go["m_Name"].AsString, ["fields"] = fields });
            }
        }
    }
}
