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
        private readonly ConcurrentDictionary<int, T> _items = new();
        private int _nextId;

        public Task<T> Get(int id)
        {
            _items.TryGetValue(id, out var item);
            return Task.FromResult(item!);
        }

        public Task Delete(int id)
        {
            _items.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        public Task Update(T item)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task Add(T newItem)
        {
            newItem.Id = GetNextId();
            _items[newItem.Id] = newItem;
            return Task.CompletedTask;
        }

        public int GetNextId()
        {
            return Interlocked.Increment(ref _nextId);
        }

        public Task<IEnumerable<T>> GetAll()
        {
            return Task.FromResult<IEnumerable<T>>(_items.Values.ToList());
        }

        public async Task Load(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var json = await File.ReadAllTextAsync(path);
            var items = JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();

            _items.Clear();
            var maxId = 0;
            foreach (var item in items)
            {
                _items[item.Id] = item;
                maxId = Math.Max(maxId, item.Id);
            }

            _nextId = maxId;
        }

        public async Task Save(string path)
        {
            var json = JsonSerializer.Serialize(_items.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
    }
}
