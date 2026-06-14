namespace TestProject.Business
{
    /// <summary>
    /// Represents an item in a file system, which can be either a file or a directory.
    /// This class implements the <see cref="IFindable"/> interface,
    /// providing properties for identification, hierarchical organization, and search
    /// optimization. The <see cref="Size"/> property is used to store the size of the file
    /// in bytes, while the <see cref="IsDirectory"/> property indicates whether the item
    /// is a directory or a file. Directories do not use the <see cref="Size"/> property.
    /// The <see cref="Parent"/> property represents the path of the parent directory of
    /// the item, and the <see cref="Path"/> property represents the full path to the item
    /// in the file system. The <see cref="Trigrams"/> property is used to store trigrams
    /// of the item's name for search optimization purposes.
    /// </summary>
    public class FileSystemItem : IFindable
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? Size { get; set; }
        public bool IsDirectory { get; set; }
        public string Parent { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string[] Trigrams { get; set; } = Array.Empty<string>();
    }
}
