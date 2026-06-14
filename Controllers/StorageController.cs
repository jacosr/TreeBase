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

        public StorageController(FileSystemStorage storage)
        {
            _storage = storage;
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
        /// Retrieves the metadata for the item with the given identifier.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<FileSystemItem>> Get(int id)
        {
            var item = await _storage.Get(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        /// <summary>
        /// Retrieves the direct children of the directory with the given identifier.
        /// </summary>
        [HttpGet("{id}/children")]
        public async Task<ActionResult<IEnumerable<FileSystemEntry>>> GetChildren(int id)
        {
            var item = await _storage.Get(id);
            if (item == null || !item.IsDirectory)
            {
                return NotFound();
            }

            return Ok(await _storage.GetChildren(id));
        }

        /// <summary>
        /// Retrieves a single item together with its file count (if it is a directory).
        /// </summary>
        [HttpGet("{id}/entry")]
        public async Task<ActionResult<FileSystemEntry>> GetEntry(int id)
        {
            var entry = await _storage.GetEntry(id);
            if (entry == null)
            {
                return NotFound();
            }

            return Ok(entry);
        }

        /// <summary>
        /// Downloads the content of the file with the given identifier.
        /// </summary>
        [HttpGet("{id}/content")]
        public async Task<IActionResult> GetContent(int id)
        {
            var item = await _storage.Get(id);
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

            var parent = await _storage.Get(request.ParentId);
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
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }

        /// <summary>
        /// Replaces the content of the file with the given identifier.
        /// </summary>
        [HttpPut("{id}/content")]
        public async Task<IActionResult> SetContent(int id, IFormFile file)
        {
            var item = await _storage.Get(id);
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
