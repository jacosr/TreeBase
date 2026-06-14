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


        /// <summary>
        /// Finds items of type <typeparamref name="T"/> based on the specified path and name. The search engine will
        ///  use the path and name to query its indexes and return a collection of matching items. Either path or name can be null, 
        /// in which case the search engine will return all items matching the non-null parameter. If both parameters are 
        /// null, it will return all items.
        /// </summary>
        /// <param name="path">The path to restrict the search to</param>
        /// <param name="name">The name or substring of the name to search for</param>
        /// <returns></returns>
        Task<IEnumerable<T>> Find(string path, string name);


        /// <summary>
        /// Deletes an item of type <typeparamref name="T"/> based on the specified identifier. The search engine will 
        /// use the identifier to locate and remove the item from all indexes.
        /// </summary>
        /// <param name="id">The identifier of the item to delete</param>
        /// <returns></returns>
        Task Delete(int id);


        /// <summary>
        /// Adds a new item of type <typeparamref name="T"/> to the search engine based on the specified identifier,
        ///  path, and name. The search engine will index it in all appropriate indexes to make it available for 
        /// future search operations. The search engine will not write to the metadata storage, only read from it. 
        /// It will only read from it when necessary (e.g. deletions).
        /// </summary>
        /// <param name="id">The identifier of the item to add</param>
        /// <param name="path">The path of the item to add</param>
        /// <param name="name">The name of the item to add</param>
        /// <returns></returns>
        Task Add(int id, string path, string name);
    }
}
