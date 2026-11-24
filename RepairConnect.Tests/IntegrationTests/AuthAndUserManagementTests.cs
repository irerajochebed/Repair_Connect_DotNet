using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairConnect.Data;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class AuthAndUserManagementTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();
    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private static async Task TokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }

    private static async Task LoginAsync(HttpClient client, string name = "admin")
    {
        await TokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = name + "@tests.invalid", password = ApiTestFactory.TestPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await TokenAsync(client);
    }

    private async Task<int> UserIdAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>().Users
            .Where(user => user.Email == name + "@tests.invalid").Select(user => user.Id).SingleAsync();
    }

    [Fact]
    public async Task PublicHomepage_IsRoutable_AndHomeNavigationTargetsRoot()
    {
        using var client = NewClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"home-title\"", html);
        Assert.Matches("<a\\b(?=[^>]*\\bhref=\"/\")[^>]*>\\s*Home\\s*</a>", html);
    }

    [Fact]
    public async Task AnonymousWorkspace_RedirectsToAccount_PreservingReturnUrl()
    {
        using var client = NewClient();
        const string requestedPath = "/Dashboard?tab=users";
        var response = await client.GetAsync(requestedPath);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var destination = new Uri(client.BaseAddress!, response.Headers.Location);
        Assert.Equal("/Account", destination.AbsolutePath);
        Assert.Equal(requestedPath, QueryHelpers.ParseQuery(destination.Query)["returnUrl"].ToString());
    }

    [Fact]
    public async Task Anonymous_ProtectedRoutes_Return401_AndLoginRequiresCsrf()
    {
        using var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
        var missingToken = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@tests.invalid", password = ApiTestFactory.TestPassword });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        Assert.Contains("Request verification failed", await missingToken.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_CreatesCookie_MeReturnsAccount_LogoutClearsAccess()
    {
        using var client = NewClient();
        await LoginAsync(client);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin@tests.invalid", body.GetProperty("email").GetString());
        Assert.Equal("Admin", body.GetProperty("roles").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.True(me.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("admin@tests.invalid", "wrong-password")]
    [InlineData("missing@tests.invalid", ApiTestFactory.TestPassword)]
    [InlineData("inactive@tests.invalid", ApiTestFactory.TestPassword)]
    public async Task InvalidOrInactiveLogin_DoesNotCreateSession(string email, string password)
    {
        using var client = NewClient();
        await TokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid email or password", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Client_CannotUseAdminEndpoints()
    {
        using var client = NewClient();
        await LoginAsync(client, "client");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users", new { displayName = "Denied", email = "denied@tests.invalid", password = ApiTestFactory.TestPassword, role = "Admin" })).StatusCode);
    }

    [Fact]
    public async Task UserDirectory_SearchFiltersAndPaginationReturnDatabaseRecords()
    {
        using var client = NewClient();
        await LoginAsync(client);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?role=Client&status=Active&search=client&page=1&pageSize=1");
        Assert.Equal(1, json.GetProperty("totalCount").GetInt32());
        Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("client@tests.invalid", json.GetProperty("items")[0].GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/users?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/users?pageSize=51")).StatusCode);
    }

    [Fact]
    public async Task AdminCreatesClient_PersistsHash_RejectsDuplicateAndMissingCsrf()
    {
        using var client = NewClient();
        await LoginAsync(client);
        var request = new { displayName = "New Client", email = "new@tests.invalid", password = ApiTestFactory.TestPassword };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/admin/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/users", request)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>().Users.SingleAsync(user => user.Email == request.email);
        Assert.NotEqual(request.password, account.PasswordHash);
        Assert.True(account.PasswordHash?.Length > 30);
        using var createdClient = NewClient();
        await LoginAsync(createdClient, "new");
        Assert.Equal(HttpStatusCode.Forbidden, (await createdClient.GetAsync("/api/admin/dashboard")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", new { displayName = "No Token", email = "notoken@tests.invalid", password = ApiTestFactory.TestPassword })).StatusCode);
    }

    [Fact]
    public async Task StatusChanges_Persist_RejectSelfAndInvalidInput_AndRevokeExistingSession()
    {
        using var admin = NewClient();
        using var account = NewClient();
        await LoginAsync(admin);
        await LoginAsync(account, "client");
        var userId = await UserIdAsync("client");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{userId}/status", new { status = "Inactive" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await account.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{userId}/status", new { status = "Active" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await account.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(account, "client");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{await UserIdAsync("admin")}/status", new { status = "Inactive" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{userId}/status", new { status = 999 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{userId}/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/users/999999/status", new { status = "Inactive" })).StatusCode);
    }

    [Fact]
    public async Task SoftDelete_DeactivatesWithoutRemovingRelatedAccount_AndCannotDeleteSelf()
    {
        using var admin = NewClient();
        await LoginAsync(admin);
        var userId = await UserIdAsync("client");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{userId}")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>().Users.SingleAsync(user => user.Id == userId);
        Assert.Equal(RepairConnect.Models.Enums.UserStatus.Inactive, account.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/admin/users/{await UserIdAsync("admin")}")).StatusCode);
    }

    [Fact]
    public async Task ChangedRoles_RevokeAnExistingAdministratorCookie()
    {
        using var admin = NewClient();
        await LoginAsync(admin);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
            var user = await db.Users.SingleAsync(account => account.Email == "admin@tests.invalid");
            user.Roles = RepairConnect.Models.Enums.UserRoles.Client;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/admin/users")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
