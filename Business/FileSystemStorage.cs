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
        private IMetadataStorage<FileSystemItem>? _metadataStorage;

        public FileSystemStorage(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            Directory.CreateDirectory(_rootDirectory);
        }

        public IMetadataStorage<FileSystemItem> MetadataStorage
        {
            set => _metadataStorage = value;
        }

        private IMetadataStorage<FileSystemItem> Metadata =>
            _metadataStorage ?? throw new InvalidOperationException($"{nameof(MetadataStorage)} has not been set.");


        /// <summary>
        /// Scans the root directory on disk and returns metadata for the root item and every file and directory
        /// beneath it. The returned items have no <see cref="FileSystemItem.Id"/> assigned; they are intended to be
        /// added to an empty <see cref="IMetadataStorage{T}"/> to (re)initialize it from the contents of the disk.
        /// </summary>
        public async Task CollectMetadata()
        {
            var root = new FileSystemItem
            {
                Name = string.Empty,
                Parent = string.Empty,
                ParentId = 0,
                Path = RootPath,
                IsDirectory = true
            };
            int rootId = await Metadata.Add(root);

            await CollectDirectory(_rootDirectory, RootPath, rootId);
        }

        private async Task CollectDirectory(string physicalPath, string itemPath, int parentId)
        {
            foreach (var directory in Directory.GetDirectories(physicalPath))
            {
                var name = Path.GetFileName(directory);
                var childPath = CombinePath(itemPath, name);
                var item = new FileSystemItem
                {
                    Name = name,
                    Parent = itemPath,
                    ParentId = parentId,
                    Path = childPath,
                    IsDirectory = true,
                };
                int itemId = await Metadata.Add(item);

                await CollectDirectory(directory, childPath, itemId);
            }

            foreach (var file in Directory.GetFiles(physicalPath))
            {
                var name = Path.GetFileName(file);
                var childPath = CombinePath(itemPath, name);
                var item = new FileSystemItem
                {
                    Name = name,
                    Parent = itemPath,
                    ParentId = parentId,
                    Path = childPath,
                    IsDirectory = false,
                    Size = (int)new FileInfo(file).Length,
                };
                await Metadata.Add(item);
            }
        }

        public async Task Delete(int id)
        {
            var item = await Metadata.Get(id);
            if (item == null)
            {
                return;
            }

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
            var newPath = $"{parent.Path}/{item.Name}";
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

        }

        public async Task Copy(int id, int parentId, string name)
        {
            var item = await Metadata.Get(id);
            var parent = await Metadata.Get(parentId);
            if (item == null || parent == null || !parent.IsDirectory || string.IsNullOrEmpty(name))
            {
                return;
            }

            var newPath = CombinePath(parent.Path, name);
            var sourcePhysicalPath = GetPhysicalPath(item.Path);
            var destPhysicalPath = GetPhysicalPath(newPath);

            if (item.IsDirectory)
            {
                if (!Directory.Exists(sourcePhysicalPath) || Directory.Exists(destPhysicalPath))
                {
                    return;
                }

                CopyDirectory(sourcePhysicalPath, destPhysicalPath);
            }
            else
            {
                if (!File.Exists(sourcePhysicalPath) || File.Exists(destPhysicalPath))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPhysicalPath)!);
                File.Copy(sourcePhysicalPath, destPhysicalPath);
            }
        }

        public async Task Save(FileSystemItem newItem)
        {
            var physicalPath = GetPhysicalPath(newItem.Path);
            if (newItem.IsDirectory)
            {
                Directory.CreateDirectory(physicalPath);
                newItem.Size = null;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
                File.WriteAllBytes(physicalPath, Array.Empty<byte>());

                newItem.Size = (int)new FileInfo(physicalPath).Length;
            }
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
