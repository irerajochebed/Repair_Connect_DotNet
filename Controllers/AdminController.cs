using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ApiControllerBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IAdminTechnicianService _technicianService;
    private readonly IUserAdminService _userService;

    public AdminController(
        IAdminDashboardService dashboardService,
        IAdminTechnicianService technicianService,
        IUserAdminService userService)
    {
        _dashboardService = dashboardService;
        _technicianService = technicianService;
        _userService = userService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardVm), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetDashboardMetricsAsync(cancellationToken);
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
        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination",
                Detail = "Page must be at least 1 and pageSize must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _technicianService.GetTechniciansAsync(status, search, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("technicians/{userId:int}")]
    [ProducesResponseType(typeof(TechnicianReviewVm), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTechnician(int userId, CancellationToken cancellationToken)
    {
        var result = await _technicianService.GetTechnicianByIdAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("technicians/{userId:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveTechnician(int userId, CancellationToken cancellationToken)
    {
        var result = await _technicianService.ApproveTechnicianAsync(userId, CurrentUserId, cancellationToken);
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _technicianService.RejectTechnicianAsync(userId, request.Reason, CurrentUserId, cancellationToken);
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
        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination",
                Detail = "Page must be at least 1 and pageSize must be between 1 and 50.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _userService.GetUsersAsync(role, status, search, page, pageSize, cancellationToken);
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userService.UpdateUserStatusAsync(id, request.Status, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("users")]
    [ProducesResponseType(typeof(UserRowVm), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] CreateAdminRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _userService.CreateAdminAsync(request, cancellationToken);
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
        var result = await _userService.SoftDeleteUserAsync(id, CurrentUserId, cancellationToken);
        return ToActionResult(result);
    }
}
