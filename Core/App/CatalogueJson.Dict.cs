namespace TerrainEditor.App;

/// <summary>
/// The dictionary kind <c>CatalogueJson</c> writes: keys kept in the order they were set.
/// </summary>
internal static partial class CatalogueJson
{
    /// <summary>
    /// A dictionary that keeps its keys in the order they came (setting a key that is there keeps its place).
    /// </summary>
    public sealed class Dict : IEnumerable<KeyValuePair<string, object?>>
    {
        /// <summary>
        /// The keys in the order they were first set.
        /// </summary>
        private readonly List<string> _keys = [];
        /// <summary>
        /// The values by key.
        /// </summary>
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        /// <summary>
        /// A key's value (throws when missing); setting a new key adds it at the end.
        /// </summary>
        public object? this[string key]
        {
            get => _values[key];
            set
            {
                if (!_values.ContainsKey(key))
                {
                    _keys.Add(key);
                }
                _values[key] = value;
            }
        }

        /// <summary>
        /// How many keys there are.
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Whether the key is there.
        /// </summary>
        public bool ContainsKey(string key) => _values.ContainsKey(key);

        /// <summary>
        /// Adds the key at the end unless it is there already; false, and nothing changed, when it is.
        /// </summary>
        public bool TryAdd(string key, object? value)
        {
            if (!_values.TryAdd(key, value))
            {
                return false;
            }
            _keys.Add(key);
            return true;
        }

        /// <summary>
        /// The key's value, if it is there.
        /// </summary>
        public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

        /// <summary>
        /// Removes a key (nothing when it is not there).
        /// </summary>
        public void Remove(string key)
        {
            if (_values.Remove(key))
            {
                _keys.Remove(key);
            }
        }

        /// <summary>
        /// The pairs in key order.
        /// </summary>
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _keys.Select(k => new KeyValuePair<string, object?>(k, _values[k])).GetEnumerator();

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
