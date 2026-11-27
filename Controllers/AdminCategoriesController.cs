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
        CancellationToken cancellationToken = default)
    {
        var result = await categories.GetCategoriesAsync(search, isActive, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategory(int id, CancellationToken cancellationToken)
    {
        var result = await categories.GetByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categories.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToActionResult(result);
        }

        var category = result.Data!;
        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ServiceCategoryVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] SaveServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categories.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await categories.DeleteAsync(id, cancellationToken);
        return ToActionResult(result);
    }
}
