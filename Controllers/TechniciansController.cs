using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

// Public technician directory used by the home page, Explore and profile views.
[Route("api/technicians")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TechniciansController(ITechnicianDirectoryService directory) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<TechnicianDirectoryItemVm>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int? categoryId,
        [FromQuery] double? minRating, CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12) =>
        ToActionResult(await directory.SearchAsync(search, categoryId, minRating, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TechnicianPublicProfileVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Profile(int id, CancellationToken cancellationToken) =>
        ToActionResult(await directory.GetProfileAsync(id, cancellationToken));

    [HttpGet("{id:int}/reviews")]
    [ProducesResponseType(typeof(PaginatedList<PublicReviewVm>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reviews(int id, CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        ToActionResult(await directory.GetReviewsAsync(id, page, pageSize, cancellationToken));
}
