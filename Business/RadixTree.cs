namespace TestProject.Business
{
    /// <summary>
    /// A radix tree (compressed prefix tree) that indexes string keys (paths) to the identifiers of the items
    /// stored at those keys. Supports efficient lookup of all identifiers whose key starts with a given prefix,
    /// which is used to find an item and all of its descendants within a hierarchical path structure.
    /// </summary>
    internal class RadixTree
    {
        private class Node
        {
            public string Label = string.Empty;
            public Dictionary<char, Node> Children { get; } = new();
            public HashSet<int> Ids { get; } = new();
        }

        private readonly Node _root = new();
        private readonly object _lock = new();

        /// <summary>
        /// Inserts an identifier under the given key.
        /// </summary>
        public void Insert(string key, int id)
        {
            lock (_lock)
            {
                Insert(_root, key, id);
            }
        }

        private static void Insert(Node node, string remaining, int id)
        {
            if (remaining.Length == 0)
            {
                node.Ids.Add(id);
                return;
            }

            var first = remaining[0];
            if (!node.Children.TryGetValue(first, out var child))
            {
                node.Children[first] = new Node { Label = remaining, Ids = { id } };
                return;
            }

            var common = CommonPrefixLength(child.Label, remaining);
            if (common == child.Label.Length)
            {
                Insert(child, remaining.Substring(common), id);
                return;
            }

            // The new key diverges partway through the existing edge, so split it.
            var splitNode = new Node { Label = child.Label.Substring(0, common) };
            child.Label = child.Label.Substring(common);
            splitNode.Children[child.Label[0]] = child;

            var remainder = remaining.Substring(common);
            if (remainder.Length == 0)
            {
                splitNode.Ids.Add(id);
            }
            else
            {
                splitNode.Children[remainder[0]] = new Node { Label = remainder, Ids = { id } };
            }

            node.Children[first] = splitNode;
        }

        /// <summary>
        /// Removes an identifier previously inserted under the given key.
        /// </summary>
        public void Remove(string key, int id)
        {
            lock (_lock)
            {
                FindExact(_root, key)?.Ids.Remove(id);
            }
        }

        /// <summary>
        /// Returns every identifier whose key starts with the given prefix, including identifiers stored at the
        /// prefix itself.
        /// </summary>
        public IEnumerable<int> SearchByPrefix(string prefix)
        {
            lock (_lock)
            {
                var node = FindByPrefix(_root, prefix);
                if (node == null)
                {
                    return Enumerable.Empty<int>();
                }

                var result = new List<int>();
                Collect(node, result);
                return result;
            }
        }

        private static Node? FindExact(Node node, string remaining)
        {
            if (remaining.Length == 0)
            {
                return node;
            }

            if (!node.Children.TryGetValue(remaining[0], out var child) || !remaining.StartsWith(child.Label))
            {
                return null;
            }

            return FindExact(child, remaining.Substring(child.Label.Length));
        }

        private static Node? FindByPrefix(Node node, string remaining)
        {
            if (remaining.Length == 0)
            {
                return node;
            }

            if (!node.Children.TryGetValue(remaining[0], out var child))
            {
                return null;
            }

            if (remaining.Length <= child.Label.Length)
            {
                return child.Label.StartsWith(remaining) ? child : null;
            }

            if (!remaining.StartsWith(child.Label))
            {
                return null;
            }

            return FindByPrefix(child, remaining.Substring(child.Label.Length));
        }

        private static void Collect(Node node, List<int> result)
        {
            result.AddRange(node.Ids);
            foreach (var child in node.Children.Values)
            {
                Collect(child, result);
            }
        }

        private static int CommonPrefixLength(string a, string b)
        {
            var max = Math.Min(a.Length, b.Length);
            var i = 0;
            while (i < max && a[i] == b[i])
            {
                i++;
            }

            return i;
        }
    }
}
