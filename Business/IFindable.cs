namespace TestProject.Business
{
    /// <summary>
    /// Represents an entity that can be identified and located using its unique identifier, name, parent, path, and
    /// associated trigrams.
    /// </summary>
    /// <remarks>This interface is designed to provide a standardized structure for objects that need to be
    /// findable or searchable. The properties expose key attributes that can be used for identification, hierarchical
    /// organization, and search optimization.</remarks>
    public interface IFindable
    {
        int Id { get; set; }
        string Name { get; set; }
        string Parent { get; set; }
        string Path { get; set; }
        string[] Trigrams { get; set; }
    }
}
