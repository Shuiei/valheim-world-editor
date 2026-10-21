using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// The LOD group the GameBundles tests give a made-up prefab: one level for each list of renderers.
/// </summary>
public static class GameBundlesLodGroup
{
    /// <summary>
    /// Adds a LOD group to a prefab with one level for each list of renderer ids given, the most detailed first. An id of 0
    /// is a renderer left out of its level.
    /// </summary>
    /// <param name="prefab">The prefab to add the LOD group to.</param>
    /// <param name="levels">The renderers' ids of each level.</param>
    public static void Add(PrefabNode prefab, params long[][] levels) =>
        prefab.Component(UnityClass.LodGroup, UnityTypes.LodGroup, new Fields
        {
            ["m_LODs"] = new List<Fields>(levels.Select(renderers => new Fields
            {
                ["renderers"] = new List<Fields>(renderers.Select(id => new Fields { ["renderer"] = Fields.Ref(id) })),
            })),
        });
}
