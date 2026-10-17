using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// What kind of object a prefab is (networked, creature, item, location) and the names it goes by.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// The prefab of a stable hash (the saves' prefab ids), if the game has it.
    /// </summary>
    public string? NameOf(int hash)
    {
        lock (_sync)
        {
            EnsureIndex();
            return _byHash!.TryGetValue(hash, out string? n) ? n : null;
        }
    }

    /// <summary>
    /// What the prefab's root has (cached per name); null when the game has no such prefab.
    /// </summary>
    public Kind? KindOf(string name)
    {
        lock (_sync)
        {
            if (_kinds.TryGetValue(name, out var k))
            {
                return k;
            }
            if (!EnsureIndex().TryGetValue(name, out var at))
            {
                return null;
            }
            try
            {
                bool znv = false, creature = false, item = false, location = false;
                // A root that cannot be read (its bundle without type trees): nothing known of it.
                foreach (var (t, _, f) in Read(at.Bundle, at.PathId) is { } root ? Components(at.Bundle, root) : [])
                {
                    if (t != (int)AssetClassID.MonoBehaviour)
                    {
                        continue;
                    }
                    znv |= Has(f, "m_persistent") && Has(f, "m_distant") && Has(f, "m_type");
                    creature |= Has(f, "m_runSpeed");
                    item |= Has(f, "m_itemData");
                    location |= Has(f, "m_exteriorRadius");
                }
                return _kinds[name] = new Kind(znv, creature, item, location);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// Whether an object has a ZNetView (a MonoBehaviour with m_persistent and m_distant).
    /// </summary>
    private bool IsNetworked(Where w)
    {
        var go = Read(w.Bundle, w.PathId);
        return go != null && Components(w.Bundle, go).Any(c => c.Type == (int)AssetClassID.MonoBehaviour && Has(c.F, "m_persistent") && Has(c.F, "m_distant"));
    }
}
