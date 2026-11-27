using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController(
    IAdminDashboardService dashboard,
    IAdminTechnicianService technicians,
    IUserAdminService users) : ApiControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await dashboard.GetDashboardMetricsAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("technicians")]
    [ProducesResponseType(typeof(PaginatedList<TechnicianReviewRowVm>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTechnicians(
        [FromQuery] VerificationStatus? status = VerificationStatus.Pending,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination",
                Detail = "Page must be at least 1 and pageSize must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await technicians.GetTechniciansAsync(status, search, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("technicians/{userId:int}")]
    [ProducesResponseType(typeof(TechnicianReviewVm), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTechnician(int userId, CancellationToken cancellationToken)
    {
        var result = await technicians.GetTechnicianByIdAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    public sealed record CategoryDecision(int[] CategoryIds, bool Approve, string? Reason);

    [HttpPut("technicians/{userId:int}/category-review")]
    public async Task<IActionResult> ReviewCategories(int userId, CategoryDecision decision, CancellationToken ct) =>
        ToActionResult(await technicians.ReviewCategoriesAsync(userId, decision.CategoryIds, decision.Approve, decision.Reason, ct));

    [HttpPut("technicians/{userId:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveTechnician(int userId, CancellationToken cancellationToken)
    {
        var result = await technicians.ApproveTechnicianAsync(userId, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("technicians/{userId:int}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectTechnician(
        int userId,
        [FromBody] RejectTechnicianRequest request,
        CancellationToken cancellationToken)
    {
        var result = await technicians.RejectTechnicianAsync(userId, request.Reason, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(PaginatedList<UserRowVm>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserRoles? role = null,
        [FromQuery] UserStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize ||
            (role.HasValue && ((int)role.Value < 0 || (int)role.Value > 7)) ||
            (status.HasValue && !Enum.IsDefined(status.Value)))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination",
                Detail = "Page must be at least 1 and pageSize must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await users.GetUsersAsync(role, status, search, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("users/{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(
        int id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.UpdateUserStatusAsync(id, request.Status!.Value, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("users")]
    [ProducesResponseType(typeof(UserRowVm), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.CreateUserAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpDelete("users/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var result = await users.SoftDeleteUserAsync(id, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }
}
