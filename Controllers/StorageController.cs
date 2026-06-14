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

        public StorageController(FileSystemStorage storage, IMetadataStorage<FileSystemItem> metadataStorage)
        {
            _storage = storage;
            _metadataStorage = metadataStorage;
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
            await _storage.Move(id, request.ParentId);
            return NoContent();
        }

        /// <summary>
        /// Copies the item with the given identifier to a new parent directory under a new name.
        /// </summary>
        [HttpPost("{id}/copy")]
        public async Task<IActionResult> Copy(int id, [FromBody] CopyRequest request)
        {
            await _storage.Copy(id, request.ParentId, request.Name);
            return NoContent();
        }

        /// <summary>
        /// Deletes the item with the given identifier.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _storage.Delete(id);
            return NoContent();
        }
    }
}
