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

        public async Task<FileSystemItem> Get(int id)
        {
            return await Metadata.Get(id);
        }

        public async Task<IEnumerable<FileSystemItem>> GetChildren(int id)
        {
            var item = await Metadata.Get(id);
            //var prefix = item.Path == FileSystemStorage.RootPath ? FileSystemStorage.RootPath : item.Path + "/";
            var ids = _pathIndex.GetImmediateChildren(item.Path);

            List<FileSystemItem> children = new();
            foreach (var childId in ids)
            {
                var child = await Metadata.Get(childId);
                if (child != null)
                {
                    children.Add(child);
                }
            }   

            return children;
        }

        public Task Add(IFindable item)
        {
            var id = item.Id;
            var path = item.Path;
            var name = item.Name;   

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
            foreach (var item in Metadata.Items)
            {
                await Add(item);
            }
        }

        public async Task<IEnumerable<FileSystemItem>> GetDescendants(int id)
        {
            var item = await Metadata.Get(id);
            if (item == null || !item.IsDirectory)
            {
                return Enumerable.Empty<FileSystemItem>();
            }

            var prefix = item.Path == FileSystemStorage.RootPath ? FileSystemStorage.RootPath : item.Path + "/";
            var ids = _pathIndex.SearchByPrefix(prefix).Where(descId => descId != id);

            var results = new List<FileSystemItem>();
            foreach (var descId in ids)
            {
                var descendant = await Metadata.Get(descId);
                if (descendant != null)
                {
                    results.Add(descendant);
                }
            }

            return results;
        }

        public async Task<IEnumerable<FileSystemItem>> Find(string? path, string? name)
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
