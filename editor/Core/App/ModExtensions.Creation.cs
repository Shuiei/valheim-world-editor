using System.Numerics;
using TerrainEditor.Editing;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The running extensions' creation rules (ICreationRules): what a mod does to objects as the game creates them,
/// done to the objects a step of the editor makes.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension has creation rules.</summary>
    public static bool HasCreationRules => _state.Hooks.Any(l => l.Fault == null && l.CreationRules.Count > 0);

    /// <summary>An object about to be made, as the creation rules see it.</summary>
    /// <param name="Prefab">Its prefab's name.</param>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Rotation">Its turn, Unity Euler degrees.</param>
    /// <param name="Scale">Its size (1: the prefab's own).</param>
    public sealed record Making(string Prefab, Vector3 Position, Vector3 Rotation, float Scale);

    /// <summary>A change the creation rules made to an object of the batch.</summary>
    /// <param name="Rotation">Its turn now.</param>
    /// <param name="Values">The values set (or taken away: a null value), over its own.</param>
    public sealed record Change(Vector3 Rotation, IReadOnlyList<ObjectField> Values);

    /// <summary>What the creation rules did to a batch.</summary>
    /// <param name="Removed">The batch's objects left out, by index.</param>
    /// <param name="Changed">The batch's objects the rules changed, by index.</param>
    /// <param name="Added">The objects added, in order, with their values.</param>
    public sealed record Creation(IReadOnlySet<int> Removed, IReadOnlyDictionary<int, Change> Changed, IReadOnlyList<CopyView> Added)
    {
        /// <summary>Whether the rules changed nothing.</summary>
        public bool None => Removed.Count == 0 && Added.Count == 0 && Changed.Count == 0;
    }

    /// <summary>
    /// Hands a batch of objects about to be made to each running extension's creation rules, those of the prefabs it
    /// handles; null when no rule handles any. A hook that throws turns its extension off (the others go on).
    /// </summary>
    /// <param name="batch">The objects, in the order made.</param>
    /// <param name="data">An object's values as they will be made (asked only of those a rule handles).</param>
    /// <param name="seed">The world's seed.</param>
    /// <param name="biome">The biome at a point.</param>
    /// <param name="height">The ground's height at a point.</param>
    public static Creation? Create(IReadOnlyList<Making> batch, Func<int, List<ObjectField>> data, int seed,
        Func<float, float, int> biome, Func<float, float, float> height)
    {
        if (batch.Count == 0 || !HasCreationRules)
        {
            return null;
        }
        HashSet<int> removed = [];
        Dictionary<int, CopyView> changed = [];
        Dictionary<int, List<ObjectField>> own = [];
        List<CopyView> added = [];
        foreach (var l in _state.Hooks)
        {
            foreach (var rules in l.CreationRules)
            {
                var handled = new Dictionary<string, bool>(StringComparer.Ordinal);
                bool Handles(string prefab)
                {
                    if (!handled.TryGetValue(prefab, out bool yes))
                    {
                        handled[prefab] = yes = Guard(l, () => rules.Covers(prefab), false);
                    }
                    return yes;
                }
                List<int> mine = [.. Enumerable.Range(0, batch.Count).Where(i => !removed.Contains(i) && Handles(batch[i].Prefab))];
                if (mine.Count == 0)
                {
                    continue;
                }
                foreach (int i in mine.Where(i => !changed.ContainsKey(i)))
                {
                    var m = batch[i];
                    own[i] = data(i);
                    changed[i] = new CopyView(m.Prefab, m.Position, m.Rotation, [.. own[i]], m.Scale);
                }
                // What the objects are before the rules: put back whole when the rules throw part way (a swap whose
                // removal is made but not its addition).
                var before = mine.ToDictionary(i => i, i => (changed[i].Rotation, Data: changed[i].Data.ToList()));
                var step = new Step([.. mine.Select(i => changed[i])], seed, biome, height);
                if (!Guard(l, () =>
                {
                    rules.Created(step);
                    return true;
                }, false))
                {
                    foreach (int i in mine)
                    {
                        var m = batch[i];
                        changed[i] = new CopyView(m.Prefab, m.Position, before[i].Rotation, before[i].Data, m.Scale);
                    }
                    continue;
                }
                foreach (int i in mine)
                {
                    if (step.Left.Contains(changed[i]))
                    {
                        removed.Add(i);
                    }
                }
                added.AddRange(step.Made);
            }
        }
        // The values a copy starts with are the object's own: only what the rules set is a change.
        Dictionary<int, Change> changes = [];
        foreach (var (i, view) in changed)
        {
            var set = view.Data.Where(f => !own[i].Contains(f)).ToList();
            if (!removed.Contains(i) && (set.Count > 0 || view.Rotation != batch[i].Rotation))
            {
                changes[i] = new Change(view.Rotation, set);
            }
        }
        return new Creation(removed, changes, added);
    }

    /// <summary>One batch as an extension's creation rules get it (ICreation).</summary>
    private sealed class Step(IReadOnlyList<CopyView> objects, int seed, Func<float, float, int> biome, Func<float, float, float> height) : ICreation
    {
        /// <summary>The objects left out.</summary>
        public HashSet<ICopy> Left { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>The objects added.</summary>
        public List<CopyView> Made { get; } = [];

        /// <inheritdoc/>
        public IReadOnlyList<ICopy> Objects => objects;

        /// <inheritdoc/>
        public int Seed => seed;

        /// <inheritdoc/>
        public void Remove(ICopy obj) => Left.Add(obj);

        /// <inheritdoc/>
        public ICopy Add(string prefab, Vector3 position, Vector3 rotation)
        {
            var made = new CopyView(prefab, position, rotation, []);
            Made.Add(made);
            return made;
        }

        /// <inheritdoc/>
        public int Biome(float x, float z) => biome(x, z);

        /// <inheritdoc/>
        public float GroundHeight(float x, float z) => height(x, z);
    }
}
