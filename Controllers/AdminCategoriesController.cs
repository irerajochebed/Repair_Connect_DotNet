using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[Route("api/admin/categories")]
[Authorize(Policy = "AdminOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCategoriesController(IServiceCategoryService categories) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ServiceCategoryVm>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories([FromQuery] string? search = null,
        [FromQuery] bool? isActive = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await categories.GetCategoriesAsync(search, isActive, page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategory(int id, CancellationToken cancellationToken) =>
        ToActionResult(await categories.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categories.CreateAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetCategory), new { id = result.Data!.Id }, result.Data)
            : ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveServiceCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await categories.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        ToActionResult(await categories.DeleteAsync(id, cancellationToken));
}
