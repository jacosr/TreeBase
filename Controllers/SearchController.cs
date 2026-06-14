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
    }
}
