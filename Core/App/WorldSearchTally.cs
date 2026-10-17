namespace TerrainEditor.App;

/// <summary>
/// The hits a world search has found so far: the first ones up to its limit, the count per kind, item or text, and
/// the total. Also the query's matching, which the searches share.
/// </summary>
internal sealed class WorldSearchTally
{
    /// <summary>The trimmed text looked for.</summary>
    private readonly string _query;

    /// <summary>How many hits to keep.</summary>
    private readonly int _limit;

    /// <summary>The hits kept, in the order found.</summary>
    private readonly List<WorldSearch.Hit> _hits = [];

    /// <summary>How many were found, per count key.</summary>
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>How many hits there were in all, kept or not.</summary>
    private int _total;

    /// <summary>Starts an empty tally for a query.</summary>
    /// <param name="query">The trimmed text looked for.</param>
    /// <param name="limit">How many hits to keep.</param>
    internal WorldSearchTally(string query, int limit)
    {
        _query = query;
        _limit = limit;
    }

    /// <summary>Whether a name or a text contains the query, ignoring case.</summary>
    /// <param name="s">The name or text.</param>
    internal bool Matches(string s) => s.Contains(_query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a name is shown: the game's own bookkeeping objects (_ZoneCtrl, _TerrainCompiler...) only when the query
    /// asks for them by name.
    /// </summary>
    /// <param name="name">The object's display name.</param>
    internal bool Shown(string name) => !name.StartsWith('_') || _query.StartsWith('_');

    /// <summary>Records a hit.</summary>
    /// <param name="id">The object's id.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="p">Its position.</param>
    /// <param name="match">What matched; null for a kind search.</param>
    /// <param name="countKey">The count it adds to.</param>
    /// <param name="amount">How much it adds (items: the stack sizes).</param>
    internal void Found(int id, string name, System.Numerics.Vector3 p, string? match, string countKey, int amount = 1)
    {
        _total++;
        _counts[countKey] = _counts.GetValueOrDefault(countKey) + amount;
        if (_hits.Count < _limit)
        {
            _hits.Add(new WorldSearch.Hit(id, name, p.X, p.Y, p.Z, match));
        }
    }

    /// <summary>The search's result so far.</summary>
    /// <returns>The hits kept, the counts, the total, and whether any were left out.</returns>
    internal WorldSearch.Result Finish() => new(_hits, _counts, _total, _total > _hits.Count);
}
