namespace TestProject.Business
{
    /// <summary>
    /// A search engine for <see cref="FileSystemItem"/> entries that maintains two indexes: a <see cref="RadixTree"/>
    /// keyed on item path (used to find an item and its descendants by path prefix) and a <see cref="TrigramIndex"/>
    /// keyed on item name (used to find items whose name contains a given substring).
    /// </summary>
    public class FileSystemSearchEngine : ISearchEngine<FileSystemItem>
    {
        private readonly RadixTree _pathIndex = new();
        private readonly TrigramIndex _nameIndex = new();
        private IMetadataStorage<FileSystemItem>? _metadataStorage;

        public IMetadataStorage<FileSystemItem> MetadataStorage
        {
            set => _metadataStorage = value;
        }

        private IMetadataStorage<FileSystemItem> Metadata =>
            _metadataStorage ?? throw new InvalidOperationException($"{nameof(MetadataStorage)} has not been set.");

        public Task Add(int id, string path, string name)
        {
            _pathIndex.Insert(path, id);
            _nameIndex.Add(id, name);
            return Task.CompletedTask;
        }

        public async Task Delete(int id)
        {
            var item = await Metadata.Get(id);
            if (item == null)
            {
                return;
            }

            _pathIndex.Remove(item.Path, id);
            _nameIndex.Remove(id);
        }

        public async Task BuildIndexes()
        {
            foreach (var item in await Metadata.GetAll())
            {
                await Add(item.Id, item.Path, item.Name);
            }
        }

        public async Task<IEnumerable<FileSystemItem>> Find(string path, string name)
        {
            IEnumerable<int>? ids = null;

            if (!string.IsNullOrEmpty(path))
            {
                ids = _pathIndex.SearchByPrefix(path).ToHashSet();
            }

            if (!string.IsNullOrEmpty(name))
            {
                var nameMatches = _nameIndex.Search(name).ToHashSet();
                ids = ids == null ? nameMatches : ids.Intersect(nameMatches).ToHashSet();
            }

            ids ??= _pathIndex.SearchByPrefix(string.Empty);

            var results = new List<FileSystemItem>();
            foreach (var id in ids)
            {
                var item = await Metadata.Get(id);
                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }
    }
}
