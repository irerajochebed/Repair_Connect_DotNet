using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Services.Common;

namespace RepairConnect.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }

    protected IActionResult ToActionResult(ServiceResult result)
    {
        if (result.Succeeded)
        {
            return NoContent();
        }

        return MapError(result.Error, result.ErrorType);
    }

    protected IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.Succeeded)
        {
            return Ok(result.Data);
        }

        return MapError(result.Error, result.ErrorType);
    }

    private IActionResult MapError(string? error, ServiceErrorType errorType)
    {
        var message = error ?? "An error occurred.";

        return errorType switch
        {
            ServiceErrorType.NotFound => NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = message,
                Status = StatusCodes.Status404NotFound
            }),
            ServiceErrorType.Conflict => Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = message,
                Status = StatusCodes.Status409Conflict
            }),
            ServiceErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = message,
                Status = StatusCodes.Status403Forbidden
            }),
            _ => BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = message,
                Status = StatusCodes.Status400BadRequest
            })
        };
    }
}
