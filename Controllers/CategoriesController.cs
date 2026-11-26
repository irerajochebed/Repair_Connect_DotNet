using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[Route("api/categories")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CategoriesController(IServiceCategoryService categories) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceCategoryVm>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken) =>
        ToActionResult(await categories.GetActiveAsync(cancellationToken));
}
