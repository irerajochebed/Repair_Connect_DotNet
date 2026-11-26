using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

// Client side of repair requests and bookings.
[Route("api")]
[Authorize(Policy = "ClientOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RequestsController(IRequestService requests, IClientBookingService bookings, IReviewService reviews)
    : ApiControllerBase
{
    [HttpPost("requests")]
    [ProducesResponseType(typeof(RepairRequestVm), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateRepairRequest request, CancellationToken cancellationToken)
    {
        var result = await requests.CreateAsync(CurrentUserId, request, cancellationToken);
        return result.Succeeded ? Created($"/api/requests/{result.Data!.Id}", result.Data) : ToActionResult(result);
    }

    [HttpGet("requests")]
    [ProducesResponseType(typeof(PaginatedList<RepairRequestVm>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        ToActionResult(await requests.GetMineAsync(CurrentUserId, page, pageSize, cancellationToken));

    [HttpGet("requests/{id:int}")]
    [ProducesResponseType(typeof(RepairRequestVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        ToActionResult(await requests.GetByIdAsync(CurrentUserId, id, cancellationToken));

    [HttpPost("requests/{id:int}/bookings")]
    [ProducesResponseType(typeof(RepairRequestVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChooseTechnician(int id, ChooseTechnicianRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await bookings.ChooseTechnicianAsync(CurrentUserId, id, request.TechnicianUserId!.Value,
            cancellationToken));

    [HttpPut("bookings/{id:int}/cancel")]
    [ProducesResponseType(typeof(RepairRequestVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken) =>
        ToActionResult(await bookings.CancelAsync(CurrentUserId, id, cancellationToken));

    [HttpPost("bookings/{id:int}/review")]
    [ProducesResponseType(typeof(PublicReviewVm), StatusCodes.Status201Created)]
    public async Task<IActionResult> Review(int id, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await reviews.CreateAsync(CurrentUserId, id, request, cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Data) : ToActionResult(result);
    }
}
