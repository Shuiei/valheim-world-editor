namespace TerrainEditor.App;

/// <summary>
/// A value worked out from a source once and kept while the source stays the same (the game's bundles reader: read
/// again for another Valheim folder). Callers at the same time wait for the same work; work that throws is not kept,
/// so the next call tries again.
/// </summary>
/// <typeparam name="TSource">What the value is read from, compared by reference.</typeparam>
/// <typeparam name="TValue">The value.</typeparam>
/// <param name="read">Works the value out from a source.</param>
public sealed class KeptRead<TSource, TValue>(Func<TSource, TValue> read)
    where TSource : class
{
    /// <summary>Guards <see cref="_kept"/>.</summary>
    private readonly Lock _lock = new();

    /// <summary>The source last asked for and its value, being worked out or done; null before the first call.</summary>
    private (TSource Source, Lazy<TValue> Value)? _kept;

    /// <summary>
    /// The value for <paramref name="source"/>: the one kept when it is the source of the last call, else worked out
    /// now on the caller's thread (the old one dropped).
    /// </summary>
    public TValue Of(TSource source)
    {
        Lazy<TValue> value;
        lock (_lock)
        {
            if (_kept is not { } k || !ReferenceEquals(k.Source, source))
            {
                _kept = (source, new Lazy<TValue>(() => read(source)));
            }
            value = _kept.Value.Value;
        }
        try
        {
            return value.Value;
        }
        catch
        {
            // Not kept: the next call works it out again.
            lock (_lock)
            {
                if (_kept is { } k && k.Value == value)
                {
                    _kept = null;
                }
            }
            throw;
        }
    }

    /// <summary>The value kept for <paramref name="source"/> without working anything out: false when there is none (yet).</summary>
    public bool TryKept(TSource source, out TValue value)
    {
        lock (_lock)
        {
            if (_kept is { } k && ReferenceEquals(k.Source, source) && k.Value.IsValueCreated)
            {
                value = k.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }
}
