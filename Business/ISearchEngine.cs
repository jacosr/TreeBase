namespace TestProject.Business
{
    /// <summary>
    /// Defines a contract for a search engine that provides functionality to add, find, and delete items  of a
    /// specified type.
    /// </summary>
    /// <remarks>This interface is designed to support operations for managing and searching items, including
    /// adding  new items, finding items based on their path and name, and deleting items by their identifier. It is assumed that the 
    /// data it is searching is hierarchical, and the path parameters refer to paths through the hierarchy to a set of items.  It will 
    /// therefore use two indexes to find items by their path and name.
    /// When necessary, it uses a metadata storage to find item paths by their id (e.g. deletions). </remarks>
    /// <typeparam name="T">The type of items managed by the search engine. Must implement the <see cref="IFindable"/> interface.</typeparam>
    public interface ISearchEngine<T> where T : IFindable
    {
        /// <summary>
        /// Sets the metadata storage used to manage and persist metadata for objects of type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>This property allows the search engine to interact with the metadata storage, primarily to find the path of 
        /// and item by the item id.  The search engine should only read the metadata</remarks>
        IMetadataStorage<T> MetadataStorage { set; }
        Task<IEnumerable<T>> Find(string path, string name);
        Task Delete(int id);
        Task Add(int id, string path, string name);
    }
}
