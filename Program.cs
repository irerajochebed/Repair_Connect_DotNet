using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RepairConnect.Controllers;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "RepairConnect.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRazorPages(options => options.Conventions.AuthorizePage("/Dashboard"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("RepairConnect");
builder.Services.AddDbContext<RepairConnectDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString)) options.UseSqlServer(connectionString);
});

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddUserStore<CustomUserStore>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
.AddDefaultTokenProviders();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "RepairConnect.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account";
        options.AccessDeniedPath = "/Account";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context => ApiOrPageRedirect(context, 401, "Authentication required.");
        options.Events.OnRedirectToAccessDenied = context => ApiOrPageRedirect(context, 403, "Access denied.");
        options.Events.OnValidatePrincipal = async context =>
        {
            // Revocation takes effect on the next request, including existing cookies.
            var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var db = context.HttpContext.RequestServices.GetRequiredService<RepairConnectDbContext>();
            var user = int.TryParse(idText, out var id)
                ? await db.Users.AsNoTracking().Where(user => user.Id == id)
                    .Select(user => new { user.Status, user.Roles })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted)
                : null;
            var expectedRoles = new HashSet<string>(StringComparer.Ordinal);
            if (user != null)
            {
                if ((user.Roles & UserRoles.Client) != 0) expectedRoles.Add("Client");
                if ((user.Roles & UserRoles.Technician) != 0) expectedRoles.Add("Technician");
                if ((user.Roles & UserRoles.Admin) != 0) expectedRoles.Add("Admin");
            }
            var cookieRoles = context.Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
                ?? Enumerable.Empty<string>();
            if (user == null || user.Status != UserStatus.Active || !expectedRoles.SetEquals(cookieRoles))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ClientOnly", policy => policy.RequireRole("Client"));
    options.AddPolicy("TechnicianOnly", policy => policy.RequireRole("Technician"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminTechnicianService, AdminTechnicianService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IAccountCreationService, AccountCreationService>();
builder.Services.AddScoped<ITechnicianApplicationService, TechnicianApplicationService>();
builder.Services.AddScoped<ITechnicianDocumentStorage, TechnicianDocumentStorage>();
builder.Services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "RepairConnect API", Version = "v1"
}));

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    logger.LogError(failure?.Error, "Request failed: {Path}", context.Request.Path);
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(statusCode: 500, title: "Service unavailable",
            detail: "The service could not complete your request. Please try again.").ExecuteAsync(context);
    }
    else context.Response.Redirect("/Error");
}));
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
    try
    {
        if (await db.Database.CanConnectAsync())
            await DbInitializer.SeedAsync(db, app.Configuration,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Initial data could not be loaded. Check the database configuration and migrations.");
    }
}
else if (!app.Environment.IsEnvironment("Testing")) app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Retain bookmarked links without adding more navigation destinations.
app.MapGet("/Services", (HttpRequest request) => RedirectPreview(request, "/Explore"));
app.MapGet("/Technicians", (HttpRequest request) => RedirectPreview(request, "/Explore"));
app.MapGet("/Login", () => Results.Redirect("/Account"));
app.MapGet("/Register", () => Results.Redirect("/Account?mode=access"));
app.MapGet("/NewRequest", (HttpRequest request) => RedirectPreview(request, "/Dashboard?new=1"));
app.MapGet("/Profile", () => Results.Redirect("/Dashboard"));
app.MapGet("/ApplyTechnician", () => Results.Redirect("/Dashboard?panel=technician"));
app.MapGet("/Privacy", () => Results.Redirect("/?privacy=1"));
app.Run();

static IResult RedirectPreview(HttpRequest request, string destination)
{
    var values = request.Query.Where(entry => entry.Key is "category" or "location" or "technician")
        .ToDictionary(entry => entry.Key, entry => (string?)entry.Value.ToString());
    return Results.Redirect(QueryHelpers.AddQueryString(destination, values));
}
static Task ApiOrPageRedirect(RedirectContext<CookieAuthenticationOptions> context, int status, string message)
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new { title = status == 401 ? "Unauthorized" : "Forbidden", status, detail = message });
}
public partial class Program { }
