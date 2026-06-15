using Microsoft.AspNetCore.Mvc;
using TestProject.Business;

namespace TestProject.Controllers
{
    /// <summary>
    /// Exposes the <see cref="IStorage{T}"/> for <see cref="FileSystemItem"/> as a REST API for managing files and
    /// directories.
    /// </summary>
    [ApiController]
    [Route("api/storage")]
    public class StorageController : ControllerBase
    {
        private readonly FileSystemStorage _storage;
        private readonly IMetadataStorage<FileSystemItem> _metadataStorage;
        private readonly ISearchEngine<FileSystemItem> _searchEngine;

        public StorageController(FileSystemStorage storage, IMetadataStorage<FileSystemItem> metadataStorage, ISearchEngine<FileSystemItem> searchEngine)
        {
            _storage = storage;
            _metadataStorage = metadataStorage;
            _searchEngine = searchEngine;
        }

        public class CreateItemRequest
        {
            public int ParentId { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
        }

        public class MoveRequest
        {
            public int ParentId { get; set; }
        }

        public class CopyRequest
        {
            public int ParentId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Downloads the content of the file with the given identifier.
        /// </summary>
        [HttpGet("{id}/content")]
        public async Task<IActionResult> GetContent(int id)
        {
            var item = await _metadataStorage.Get(id);
            if (item == null || item.IsDirectory)
            {
                return NotFound();
            }

            var stream = _storage.OpenRead(item);
            if (stream == null)
            {
                return NotFound();
            }

            return File(stream, "application/octet-stream", item.Name);
        }

        /// <summary>
        /// Creates a new file or directory under the given parent directory.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<FileSystemItem>> Create([FromBody] CreateItemRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }

            var parent = await _metadataStorage.Get(request.ParentId);
            if (parent == null || !parent.IsDirectory)
            {
                return BadRequest("Parent must refer to an existing directory.");
            }

            var path = parent.Path == FileSystemStorage.RootPath
                ? FileSystemStorage.RootPath + request.Name
                : parent.Path + "/" + request.Name;

            var item = new FileSystemItem
            {
                Name = request.Name,
                Parent = parent.Path,
                Path = path,
                IsDirectory = request.IsDirectory,
            };

            await _storage.Save(item);
            await _metadataStorage.Add(item);
            await _searchEngine.Add(item);

            return CreatedAtAction("Get", "Search", new { id = item.Id }, item);
        }

        /// <summary>
        /// Replaces the content of the file with the given identifier.
        /// </summary>
        [HttpPut("{id}/content")]
        public async Task<IActionResult> SetContent(int id, IFormFile file)
        {
            var item = await _metadataStorage.Get(id);
            if (item == null || item.IsDirectory)
            {
                return NotFound();
            }

            using var stream = file.OpenReadStream();
            await _storage.WriteContent(item, stream);
            return NoContent();
        }

        /// <summary>
        /// Moves the item with the given identifier to a new parent directory.
        /// </summary>
        [HttpPut("{id}/move")]
        public async Task<IActionResult> Move(int id, [FromBody] MoveRequest request)
        {
            var item = await _metadataStorage.Get(id);
            var parent = await _metadataStorage.Get(request.ParentId);

            string oldPath = item.Path;
            string newPath = $"{parent.Path}/{item.Name}";
            if (newPath == oldPath)
            {
                return NoContent();
            }

            var descendants = item.IsDirectory ? await GetDescendants(item) : [];

            await _storage.Move(id, request.ParentId);

            item.Parent = parent.Path;
            item.Path = newPath;

            await _metadataStorage.Update(item);
            await _searchEngine.Delete(item.Id);
            await _searchEngine.Add(item);

            foreach (var descendant in descendants)
            {
                descendant.Parent = descendant.Parent.Replace(oldPath, newPath);
                descendant.Path = $"{descendant.Parent}/{descendant.Name}";
                await _metadataStorage.Update(descendant);
                await _searchEngine.Delete(descendant.Id);
                await _searchEngine.Add(descendant);
            }

            return NoContent();
        }

        /// <summary>
        /// Copies the item with the given identifier to a new parent directory under a new name.
        /// </summary>
        [HttpPost("{id}/copy")]
        public async Task<IActionResult> Copy(int id, [FromBody] CopyRequest request)
        {
            await _storage.Copy(id, request.ParentId, request.Name);

            var item = await _metadataStorage.Get(id);
            var parent = await _metadataStorage.Get(request.ParentId);

            var copy = new FileSystemItem
            {
                Name = request.Name,
                Parent = parent.Path,
                Path = parent.Path + "/" + request.Name,
                IsDirectory = item.IsDirectory,
                Size = item.IsDirectory ? null : item.Size,
                Trigrams = TrigramIndex.GetTrigrams(request.Name).ToArray(),
            };
            await _metadataStorage.Add(copy);
            await _searchEngine.Add(copy);

            if (item.IsDirectory)
            {
                foreach (var descendant in await GetDescendants(item))
                {
                    var descendantCopyParent = descendant.Parent.Replace(item.Path, copy.Path);
                    var descendantCopy = new FileSystemItem
                    {
                        Name = descendant.Name,
                        Parent = descendantCopyParent,
                        Path = $"{descendantCopyParent}/{descendant.Name}",
                        IsDirectory = descendant.IsDirectory,
                        Size = descendant.IsDirectory ? null : descendant.Size,
                        Trigrams = descendant.Trigrams,
                    };
                    await _metadataStorage.Add(descendantCopy);
                    await _searchEngine.Add(descendantCopy);
                }
            }
            return NoContent();
        }

        /// <summary>
        /// Deletes the item with the given identifier.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _storage.Delete(id);
            var item = await _metadataStorage.Get(id);

            var descendants = item.IsDirectory ? await GetDescendants(item) : [];
            foreach (var descendant in descendants)
            {
                await _searchEngine.Delete(descendant.Id);
                await _metadataStorage.Delete(descendant.Id);
            }

            await _searchEngine.Delete(item.Id);
            await _metadataStorage.Delete(item.Id);

            return NoContent();
        }

        private async Task<List<FileSystemItem>> GetDescendants(FileSystemItem item)
        {
            var descendants = await _searchEngine.GetDescendants(item.Id);
            return descendants.ToList();
        }
    }
}
