namespace TestProject.Business
{
    /// <summary>
    /// A file-system-backed implementation of <see cref="IStorage{T}"/> for <see cref="FileSystemItem"/>. Items are
    /// persisted as real files and directories under a root directory on disk, with their metadata kept in sync via
    /// <see cref="MetadataStorage"/> and their search indexes kept in sync via the supplied <see cref="ISearchEngine{T}"/>.
    /// </summary>
    public class FileSystemStorage : IStorage<FileSystemItem>
    {
        public const string RootPath = "/";

        private readonly string _rootDirectory;
        private readonly ISearchEngine<FileSystemItem> _searchEngine;
        private IMetadataStorage<FileSystemItem>? _metadataStorage;

        public FileSystemStorage(string rootDirectory, ISearchEngine<FileSystemItem> searchEngine)
        {
            _rootDirectory = rootDirectory;
            _searchEngine = searchEngine;
            Directory.CreateDirectory(_rootDirectory);
        }

        public IMetadataStorage<FileSystemItem> MetadataStorage
        {
            set => _metadataStorage = value;
        }

        private IMetadataStorage<FileSystemItem> Metadata =>
            _metadataStorage ?? throw new InvalidOperationException($"{nameof(MetadataStorage)} has not been set.");

        /// <summary>
        /// Ensures a root directory item exists in metadata storage and returns it.
        /// </summary>
        public async Task<FileSystemItem> EnsureRoot()
        {
            var existing = (await Metadata.GetAll()).FirstOrDefault(i => i.Path == RootPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new FileSystemItem
            {
                Name = string.Empty,
                Parent = string.Empty,
                Path = RootPath,
                IsDirectory = true,
            };

            await Metadata.Add(root);
            await _searchEngine.Add(root.Id, root.Path, root.Name);
            return root;
        }

        public async Task<FileSystemItem> Get(int id)
        {
            var item = await Metadata.Get(id);
            if (item == null)
            {
                return item!;
            }

            if (!item.IsDirectory)
            {
                var physicalPath = GetPhysicalPath(item.Path);
                item.Size = File.Exists(physicalPath) ? (int)new FileInfo(physicalPath).Length : item.Size;
            }

            return item;
        }

        public async Task Delete(int id)
        {
            var item = await Metadata.Get(id);
            if (item == null)
            {
                return;
            }

            var descendants = item.IsDirectory ? await GetDescendants(item) : new List<FileSystemItem>();

            var physicalPath = GetPhysicalPath(item.Path);
            if (item.IsDirectory)
            {
                if (Directory.Exists(physicalPath))
                {
                    Directory.Delete(physicalPath, recursive: true);
                }
            }
            else if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            foreach (var descendant in descendants)
            {
                await _searchEngine.Delete(descendant.Id);
                await Metadata.Delete(descendant.Id);
            }

            await _searchEngine.Delete(item.Id);
            await Metadata.Delete(item.Id);
        }

        public async Task Move(int id, int parentId)
        {
            var item = await Metadata.Get(id);
            var parent = await Metadata.Get(parentId);
            if (item == null || parent == null || !parent.IsDirectory || item.Path == RootPath)
            {
                return;
            }

            var oldPath = item.Path;
            var newPath = CombinePath(parent.Path, item.Name);
            if (newPath == oldPath)
            {
                return;
            }

            var oldPhysicalPath = GetPhysicalPath(oldPath);
            var newPhysicalPath = GetPhysicalPath(newPath);

            if (item.IsDirectory)
            {
                if (Directory.Exists(oldPhysicalPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPhysicalPath)!);
                    Directory.Move(oldPhysicalPath, newPhysicalPath);
                }
            }
            else if (File.Exists(oldPhysicalPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPhysicalPath)!);
                File.Move(oldPhysicalPath, newPhysicalPath);
            }

            var descendants = item.IsDirectory ? await GetDescendants(item) : new List<FileSystemItem>();

            await _searchEngine.Delete(item.Id);
            item.Parent = parent.Path;
            item.Path = newPath;
            await Metadata.Update(item);
            await _searchEngine.Add(item.Id, item.Path, item.Name);

            foreach (var descendant in descendants)
            {
                await _searchEngine.Delete(descendant.Id);
                descendant.Path = newPath + descendant.Path.Substring(oldPath.Length);
                descendant.Parent = GetParentPath(descendant.Path);
                await Metadata.Update(descendant);
                await _searchEngine.Add(descendant.Id, descendant.Path, descendant.Name);
            }
        }

        public async Task Copy(int id, int parentId, string name)
        {
            var item = await Metadata.Get(id);
            var parent = await Metadata.Get(parentId);
            if (item == null || parent == null || !parent.IsDirectory || string.IsNullOrEmpty(name))
            {
                return;
            }

            var siblings = await Metadata.GetAll();
            if (siblings.Any(i => i.Parent == parent.Path && i.Name == name))
            {
                return;
            }

            var newPath = CombinePath(parent.Path, name);
            var sourcePhysicalPath = GetPhysicalPath(item.Path);
            var destPhysicalPath = GetPhysicalPath(newPath);

            if (item.IsDirectory)
            {
                if (!Directory.Exists(sourcePhysicalPath))
                {
                    return;
                }

                CopyDirectory(sourcePhysicalPath, destPhysicalPath);
            }
            else
            {
                if (!File.Exists(sourcePhysicalPath))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPhysicalPath)!);
                File.Copy(sourcePhysicalPath, destPhysicalPath);
            }

            var copy = new FileSystemItem
            {
                Name = name,
                Parent = parent.Path,
                Path = newPath,
                IsDirectory = item.IsDirectory,
                Size = item.IsDirectory ? null : item.Size,
                Trigrams = TrigramIndex.GetTrigrams(name).ToArray(),
            };
            await Metadata.Add(copy);
            await _searchEngine.Add(copy.Id, copy.Path, copy.Name);

            if (item.IsDirectory)
            {
                foreach (var descendant in await GetDescendants(item))
                {
                    var descendantCopyPath = newPath + descendant.Path.Substring(item.Path.Length);
                    var descendantCopy = new FileSystemItem
                    {
                        Name = descendant.Name,
                        Parent = GetParentPath(descendantCopyPath),
                        Path = descendantCopyPath,
                        IsDirectory = descendant.IsDirectory,
                        Size = descendant.IsDirectory ? null : descendant.Size,
                        Trigrams = descendant.Trigrams,
                    };
                    await Metadata.Add(descendantCopy);
                    await _searchEngine.Add(descendantCopy.Id, descendantCopy.Path, descendantCopy.Name);
                }
            }
        }

        public async Task Save(FileSystemItem newItem)
        {
            var isUpdate = newItem.Id != 0 && await Metadata.Get(newItem.Id) != null;
            if (isUpdate)
            {
                await _searchEngine.Delete(newItem.Id);
            }

            var physicalPath = GetPhysicalPath(newItem.Path);
            if (newItem.IsDirectory)
            {
                Directory.CreateDirectory(physicalPath);
                newItem.Size = null;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
                if (!File.Exists(physicalPath))
                {
                    File.WriteAllBytes(physicalPath, Array.Empty<byte>());
                }

                newItem.Size = (int)new FileInfo(physicalPath).Length;
            }

            newItem.Trigrams = TrigramIndex.GetTrigrams(newItem.Name).ToArray();

            if (isUpdate)
            {
                await Metadata.Update(newItem);
            }
            else
            {
                await Metadata.Add(newItem);
            }

            await _searchEngine.Add(newItem.Id, newItem.Path, newItem.Name);
        }

        /// <summary>
        /// Writes the given content to the file backing <paramref name="item"/> and updates its recorded size.
        /// </summary>
        public async Task WriteContent(FileSystemItem item, Stream content)
        {
            if (item.IsDirectory)
            {
                throw new InvalidOperationException("Cannot write content to a directory.");
            }

            var physicalPath = GetPhysicalPath(item.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            using (var fileStream = File.Create(physicalPath))
            {
                await content.CopyToAsync(fileStream);
            }

            item.Size = (int)new FileInfo(physicalPath).Length;
            await Metadata.Update(item);
        }

        /// <summary>
        /// Opens the file backing <paramref name="item"/> for reading, or <see langword="null"/> if it does not exist.
        /// </summary>
        public Stream? OpenRead(FileSystemItem item)
        {
            if (item.IsDirectory)
            {
                return null;
            }

            var physicalPath = GetPhysicalPath(item.Path);
            return File.Exists(physicalPath) ? File.OpenRead(physicalPath) : null;
        }

        private async Task<List<FileSystemItem>> GetDescendants(FileSystemItem item)
        {
            var prefix = item.Path == RootPath ? RootPath : item.Path + "/";
            var all = await Metadata.GetAll();
            return all.Where(i => i.Path != item.Path && i.Path.StartsWith(prefix)).ToList();
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }
        }

        private string GetPhysicalPath(string itemPath)
        {
            if (itemPath == RootPath)
            {
                return _rootDirectory;
            }

            var relative = itemPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_rootDirectory, relative);
        }

        private static string CombinePath(string parentPath, string name)
        {
            return parentPath == RootPath ? RootPath + name : parentPath + "/" + name;
        }

        private static string GetParentPath(string path)
        {
            var index = path.LastIndexOf('/');
            return index <= 0 ? RootPath : path.Substring(0, index);
        }
    }
}
