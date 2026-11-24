using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RepairConnect.Controllers;

// All cookie-authenticated API writes, including login, use a same-origin token.
public sealed class ApiAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) &&
            !HttpMethods.IsOptions(method) && !HttpMethods.IsTrace(method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Title = "Request verification failed",
                    Detail = "Refresh the page and try again.",
                    Status = StatusCodes.Status400BadRequest
                });
                return;
            }
        }

        await next();
    }
}
