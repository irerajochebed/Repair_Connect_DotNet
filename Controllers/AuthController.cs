using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountCreationService _accountCreation;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IAccountCreationService accountCreation,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IAntiforgery antiforgery)
    {
        _userManager = userManager;
        _accountCreation = accountCreation;
        _claimsFactory = claimsFactory;
        _antiforgery = antiforgery;
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthUserVm), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Already signed in",
                Detail = "Sign out before creating another account.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var result = await _accountCreation.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToActionResult(result);
        }

        var user = result.Data!;
        var principal = await _claimsFactory.CreateAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal,
            new AuthenticationProperties { IsPersistent = false });

        return Created("/api/auth/me", ToAuthUserVm(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user == null || user.Status == UserStatus.Inactive)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var principal = await _claimsFactory.CreateAsync(user);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
            ExpiresUtc = request.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
        };

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, authProperties);

        return Ok(ToAuthUserVm(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "User not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(ToAuthUserVm(user));
    }

    private static AuthUserVm ToAuthUserVm(ApplicationUser user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email ?? string.Empty,
        DateOfBirth = user.DateOfBirth,
        PhoneNumber = user.PhoneNumber,
        Roles = user.Roles,
        Status = user.Status
    };
}
