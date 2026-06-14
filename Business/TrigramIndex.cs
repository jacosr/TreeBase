namespace TestProject.Business
{
    /// <summary>
    /// An index that maps three-character substrings (trigrams) of item names to the identifiers of the items
    /// whose name contains that trigram. This allows substring searches over names without scanning every item:
    /// candidate identifiers are found by intersecting the sets for each trigram in the search term, and then
    /// verified with a direct substring check.
    /// </summary>
    internal class TrigramIndex
    {
        private readonly Dictionary<string, HashSet<int>> _trigramToIds = new();
        private readonly Dictionary<int, string> _names = new();
        private readonly object _lock = new();

        /// <summary>
        /// Indexes the given name under the specified identifier.
        /// </summary>
        public void Add(int id, string name)
        {
            lock (_lock)
            {
                var normalized = Normalize(name);
                _names[id] = normalized;

                foreach (var trigram in GetTrigrams(normalized))
                {
                    if (!_trigramToIds.TryGetValue(trigram, out var ids))
                    {
                        ids = new HashSet<int>();
                        _trigramToIds[trigram] = ids;
                    }

                    ids.Add(id);
                }
            }
        }

        /// <summary>
        /// Removes the identifier from the index.
        /// </summary>
        public void Remove(int id)
        {
            lock (_lock)
            {
                if (!_names.TryGetValue(id, out var normalized))
                {
                    return;
                }

                foreach (var trigram in GetTrigrams(normalized))
                {
                    if (_trigramToIds.TryGetValue(trigram, out var ids))
                    {
                        ids.Remove(id);
                        if (ids.Count == 0)
                        {
                            _trigramToIds.Remove(trigram);
                        }
                    }
                }

                _names.Remove(id);
            }
        }

        /// <summary>
        /// Returns the identifiers of every indexed item whose name contains the given substring.
        /// </summary>
        public IEnumerable<int> Search(string query)
        {
            lock (_lock)
            {
                var normalized = Normalize(query);
                if (normalized.Length == 0)
                {
                    return _names.Keys.ToList();
                }

                if (normalized.Length < 3)
                {
                    // Trigrams can't represent queries shorter than 3 characters, so fall back to a scan.
                    return _names.Where(kv => kv.Value.Contains(normalized)).Select(kv => kv.Key).ToList();
                }

                HashSet<int>? candidates = null;
                foreach (var trigram in GetTrigrams(normalized))
                {
                    if (!_trigramToIds.TryGetValue(trigram, out var ids))
                    {
                        return Enumerable.Empty<int>();
                    }

                    candidates = candidates == null ? new HashSet<int>(ids) : candidates.Intersect(ids).ToHashSet();
                    if (candidates.Count == 0)
                    {
                        return Enumerable.Empty<int>();
                    }
                }

                return (candidates ?? new HashSet<int>())
                    .Where(id => _names.TryGetValue(id, out var name) && name.Contains(normalized))
                    .ToList();
            }
        }

        /// <summary>
        /// Splits the given value into its constituent trigrams (lower-cased three-character substrings).
        /// </summary>
        public static IEnumerable<string> GetTrigrams(string value)
        {
            var normalized = Normalize(value);
            for (var i = 0; i <= normalized.Length - 3; i++)
            {
                yield return normalized.Substring(i, 3);
            }
        }

        private static string Normalize(string value) => value.ToLowerInvariant();
    }
}
