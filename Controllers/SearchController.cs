using Microsoft.AspNetCore.Mvc;
using TestProject.Business;

namespace TestProject.Controllers
{
    /// <summary>
    /// Exposes the <see cref="ISearchEngine{T}"/> for <see cref="FileSystemItem"/> as a REST API.
    /// </summary>
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchEngine<FileSystemItem> _searchEngine;

        public SearchController(ISearchEngine<FileSystemItem> searchEngine)
        {
            _searchEngine = searchEngine;
        }

        /// <summary>
        /// Finds items by path prefix and/or a substring of their name. Either parameter may be omitted; if both
        /// are omitted, every item is returned.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FileSystemItem>>> Find([FromQuery] string? path, [FromQuery] string? name)
        {
            var results = await _searchEngine.Find(path!, name!);
            return Ok(results);
        }

        /// <summary>
        /// Retrieves the metadata for the item with the given identifier.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<FileSystemItem>> Get(int id)
        {
            var item = await _searchEngine.Get(id);
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
        public async Task<ActionResult<IEnumerable<FileSystemItem>>> GetChildren(int id)
        {
            var item = await _searchEngine.Get(id);
            if (item == null || !item.IsDirectory)
            {
                return NotFound();
            }

            return Ok(await _searchEngine.GetChildren(id));
        }
    }
}
