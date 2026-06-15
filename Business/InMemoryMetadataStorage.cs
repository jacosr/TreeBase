using System.Collections.Concurrent;
using System.Text.Json;

namespace TestProject.Business
{
    /// <summary>
    /// An in-memory implementation of <see cref="IMetadataStorage{T}"/>. Items are kept in memory for fast access
    /// and can optionally be persisted to, and reloaded from, a JSON file via <see cref="Save(string)"/> and
    /// <see cref="Load(string)"/>.
    /// </summary>
    /// <typeparam name="T">The type of items managed by the storage. Must implement <see cref="IFindable"/>.</typeparam>
    public class InMemoryMetadataStorage<T> : IMetadataStorage<T> where T : IFindable
    {
        /// <summary>
        /// A single entry in the append-only metadata log, recording the action that was performed (Add, Update,
        /// or Delete) and either the affected item (for Add/Update) or its identifier (for Delete).
        /// </summary>
        private class LogEntry
        {
            public string Action { get; set; } = string.Empty;
            public int Id { get; set; }
            public T? Item { get; set; }
        }

        private readonly ConcurrentDictionary<int, T> _items = new();
        private readonly SemaphoreSlim _logLock = new(1, 1);
        private int _nextId;
        private string? _logPath;


        public bool IsEmpty => _items.IsEmpty;
        
        public IEnumerable<T> Items => _items.Values;


        public Task<T> Get(int id)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item!);
        }

        public async Task Delete(int id)
        {
            _items.TryRemove(id, out _);
            await AppendLog("Delete", id, default);
        }

        public async Task Update(T item)
        {
            item.Trigrams = TrigramIndex.GetTrigrams(item.Name).ToArray();
            _items[item.Id] = item;
            await AppendLog("Update", item.Id, item);
        }

        public async Task Add(T newItem)
        {
            newItem.Id = GetNextId();
            newItem.Trigrams = TrigramIndex.GetTrigrams(newItem.Name).ToArray();
            _items[newItem.Id] = newItem;
            await AppendLog("Add", newItem.Id, newItem);
        }

        public int GetNextId()
        {
            return Interlocked.Increment(ref _nextId);
        }

        public Task<IEnumerable<T>> GetAll()
        {
            return Task.FromResult<IEnumerable<T>>(_items.Values.ToList());
        }

        /// <summary>
        /// Replays the append-only metadata log at <paramref name="path"/> to rebuild the in-memory state, and
        /// remembers the path so that subsequent <see cref="Add"/>, <see cref="Update"/>, and <see cref="Delete"/>
        /// calls are appended to it.
        /// </summary>
        public async Task Load(string path)
        {
            _logPath = path;
            _items.Clear();
            _nextId = 0;

            if (!File.Exists(path))
            {
                return;
            }

            foreach (var line in await File.ReadAllLinesAsync(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize<LogEntry>(line);
                if (entry == null)
                {
                    continue;
                }

                switch (entry.Action)
                {
                    case "Add":
                    case "Update":
                        if (entry.Item != null)
                        {
                            _items[entry.Item.Id] = entry.Item;
                            _nextId = Math.Max(_nextId, entry.Item.Id);
                        }
                        break;
                    case "Delete":
                        _items.TryRemove(entry.Id, out _);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes a compacted snapshot of the current state to <paramref name="path"/> as a sequence of "Add"
        /// entries (one per item), and remembers the path so that subsequent <see cref="Add"/>, <see cref="Update"/>,
        /// and <see cref="Delete"/> calls are appended to it.
        /// </summary>
        public async Task Save(string path)
        {
            _logPath = path;
            var lines = _items.Values.Select(item => JsonSerializer.Serialize(new LogEntry { Action = "Add", Id = item.Id, Item = item }));
            await File.WriteAllLinesAsync(path, lines);
        }

        private async Task AppendLog(string action, int id, T? item)
        {
            if (_logPath == null)
            {
                return;
            }

            var entry = new LogEntry { Action = action, Id = id, Item = item };
            var line = JsonSerializer.Serialize(entry);

            await _logLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logPath, line + Environment.NewLine);
            }
            finally
            {
                _logLock.Release();
            }
        }
    }
}
