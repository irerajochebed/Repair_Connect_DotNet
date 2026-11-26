using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[Route("api/technician-application")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TechnicianApplicationController(ITechnicianApplicationService applications) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ClientOnly")]
    [ProducesResponseType(typeof(TechnicianApplicationVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await applications.GetAsync(CurrentUserId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "ClientOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(7 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [ProducesResponseType(typeof(TechnicianApplicationVm), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TechnicianApplicationVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit([FromForm] TechnicianApplicationRequest request, CancellationToken cancellationToken)
    {
        var result = await applications.SubmitAsync(CurrentUserId, request, cancellationToken);
        if (!result.Succeeded) return ToActionResult(result);
        return result.Data!.Created ? Created("/api/technician-application", result.Data.Application) : Ok(result.Data.Application);
    }

    [HttpGet("documents/{id:int}")]
    public async Task<IActionResult> Document(int id, CancellationToken cancellationToken)
    {
        var result = await applications.GetDocumentAsync(id, CurrentUserId, User.IsInRole("Admin"), cancellationToken);
        if (!result.Succeeded) return ToActionResult(result);
        Response.Headers.XContentTypeOptions = "nosniff";
        var document = result.Data!;
        return File(document.Content, document.ContentType, document.FileName);
    }
}
