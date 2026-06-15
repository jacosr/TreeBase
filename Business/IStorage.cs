using System.Runtime.Remoting;
using System.Security.AccessControl;

namespace TestProject.Business
{
    /// <summary>
    /// Defines a contract for permanent storage operations on objects of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>This interface provides methods for retrieving, deleting, moving, copying, and saving
    /// objects.  It is assumed that this storage mechanism is hierarchical and that the Parent property of the item defines the 
    /// path to the item and that the Name property defines the name of the stored item.  Implementations of this interface are 
    /// expected to handle the underlying storage mechanism.</remarks>
    /// <typeparam name="T">The type of objects managed by the storage. Must implement <see cref="IFindable"/>.</typeparam>
    public interface IStorage<T> where T : IFindable
    {
        /// <summary>
        /// Sets the metadata storage for objects of type <typeparamref name="T"/>. This property allows the storage to interact 
        /// with the metadata storage, so that it can find an item's path by it's id.  This enables storage to perform operations such as 
        /// retrieval, deletion, moving, copying objects based the item id. The metadata storage is responsible for managing the metadata 
        /// associated with the objects, while the main storage handles the actual data storage and retrieval. Storage only reads 
        /// metadata from the metadata storage and does not modify it.  
        /// The metadata storage is expected to be set before any operations that require access to metadata are performed.</remarks>
        /// </summary>
        IMetadataStorage<T> MetadataStorage { set; }


        /// <summary>
        /// Deletes the entity with the specified identifier.
        /// </summary>
        /// <remarks>This method performs an asynchronous operation to delete an entity identified by the
        /// given <paramref name="id"/>. If necessary to ensure unexpected behavior, it ensures the entity exists 
        /// before deleting it</remarks>
        /// <param name="id">The unique identifier of the entity to delete. Must be a positive integer.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task Delete(int id);


        /// <summary>
        /// Moves an item to a new parent within the hierarchy. 
        /// </summary>
        /// <remarks>This method updates the item's position in the hierarchy by assigning it to a new
        /// parent.  It ensures that both <paramref name="id"/> and <paramref name="parentId"/> refer to valid items in the
        /// hierarchy before performing the move.  If either are missing, it does nothing.</remarks> 
        /// <param name="id">The unique identifier of the item to move.</param>
        /// <param name="parentId">The unique identifier of the new parent item. Must be a valid parent within the hierarchy.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task Move(int id, int parentId);


        /// <summary>
        /// Copies an item to a new location with the specified parent and name.
        /// </summary>
        /// <remarks>This method creates a copy of the item identified by <paramref name="id"/> and places it under the parent 
        /// specified by <paramref name="parentId"/>.  Before performing the operation, it ensures that the original item exists, 
        /// that the new parent is valid, that the new name is not empty or null, and that the new name is not already used under 
        /// the new parent.  If any of these conditions are not met, the method will not perform the copy operation. If the copy is 
        /// successful, the new item will be created with a unique identifier and will be associated with the specified parent and name.
        /// The copied item will be assigned the name provided in <paramref name="name"/>.  Children of the original item will also be 
        /// copied recursively, maintaining the same structure and names under the new parent.  The method ensures that the copy operation 
        /// does not violate any constraints of the storage system, such as unique name requirements or hierarchical integrity.
        /// </remarks>
        /// <param name="id">The unique identifier of the item to copy.</param>
        /// <param name="parentId">The unique identifier of the parent under which the copied item will be placed.</param>
        /// <param name="name">The name to assign to the copied item.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        Task Copy(int id, int parentId, string name);


        /// <summary>
        /// Saves the specified item to the underlying data store in accordance with the Path property.
        /// </summary>
        /// <remarks>This method performs an asynchronous operation to save the provided item. The item must have a valid Path 
        /// property that indicates where it should be stored in the hierarchy. The method ensures that the item is saved correctly 
        /// according to its Path.  If the item already exists, it will be updated with the new values; if it does not exist, it will 
        /// be created as a new entry in the storage.</remarks>
        /// <param name="newItem">The item to be saved. Cannot be <see langword="null"/>.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task Save(T newItem);


        /// <summary>
        /// Collects metadata for all items in the storage. This method retrieves metadata information for all items currently stored,
        /// which can be used for various purposes such as indexing, searching, or displaying item information
        /// </summary>
        /// <returns>A task that represents the asynchronous metadata collection operation.
        /// This function is called to initialize the metadata for all items in the storage when the application starts and there is 
        /// no existing metadata available, or if the metadata needs to be refreshed.  It should return a collection of all items in 
        /// the storage, with their metadata (e.g. id, path, name) filled in.  This allows the search engine to build its indexes based 
        /// on the metadata of all items in the storage.</returns>
        /// </returns>
        /// <returns></returns>
        Task CollectMetadata();
    }
}
