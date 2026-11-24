using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class RegistrationTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();
    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });
    private static Dictionary<string, object?> Request(string email = "registered@tests.invalid") => new()
    {
        ["displayName"] = "Registered User", ["email"] = email,
        ["phoneNumber"] = "0780000000", ["password"] = ApiTestFactory.TestPassword
    };
    private static async Task TokenAsync(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task LoginAsync(HttpClient client, string email)
    {
        await TokenAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiTestFactory.TestPassword })).StatusCode);
        await TokenAsync(client);
    }
    private async Task<(int Users, int Profiles)> CountsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        return (await db.Users.CountAsync(), await db.TechnicianProfiles.CountAsync());
    }
    private async Task<ApplicationUser> UserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>().Users.AsNoTracking()
            .Include(user => user.TechnicianProfile).SingleAsync(user => user.NormalizedEmail == email.ToUpperInvariant());
    }
    private static void AssertSafeAccount(JsonElement account, string email, string role = "Client")
    {
        Assert.Equal(email, account.GetProperty("email").GetString());
        Assert.Equal(role, account.GetProperty("roles").GetString());
        Assert.Equal("Active", account.GetProperty("status").GetString());
        Assert.False(account.TryGetProperty("password", out _));
        Assert.False(account.TryGetProperty("passwordHash", out _));
        Assert.DoesNotContain(ApiTestFactory.TestPassword, account.GetRawText());
    }

    [Fact]
    public async Task Registration_CreatesRealHashedClientAndSession_WithoutInjectedPrivileges()
    {
        using var client = NewClient();
        await TokenAsync(client);
        var request = Request("  NewClient@tests.invalid  ");
        request["displayName"] = "  New Client  "; request["phoneNumber"] = "  0780000001  ";
        request["role"] = "Admin"; request["roles"] = "Admin, Technician"; request["status"] = "Inactive";
        request["isAdmin"] = true; request["specialization"] = "Forged technician";
        request["technicianProfile"] = new { verificationStatus = "Approved", availabilityStatus = "Available" };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertSafeAccount(await response.Content.ReadFromJsonAsync<JsonElement>(), "NewClient@tests.invalid");
        var user = await UserAsync("NewClient@tests.invalid");
        Assert.Equal("New Client", user.DisplayName); Assert.Equal("0780000001", user.PhoneNumber);
        Assert.Equal(UserRoles.Client, user.Roles); Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.TechnicianProfile); Assert.NotEqual(ApiTestFactory.TestPassword, user.PasswordHash);
        Assert.True(user.PasswordHash?.Length > 30);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(user.Id, me.GetProperty("id").GetInt32());
        AssertSafeAccount(me, "NewClient@tests.invalid");
        Assert.Contains("<span data-greeting-name>New Client</span>", await client.GetStringAsync("/Dashboard"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
        await TokenAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(client, "  NEWCLIENT@TESTS.INVALID  ");
        AssertSafeAccount(await client.GetFromJsonAsync<JsonElement>("/api/auth/me"), "NewClient@tests.invalid");
    }

    [Fact]
    public async Task PublicRegistration_AlwaysCreatesClient_IgnoringAnyInjectedRole()
    {
        var index = 0;
        foreach (var role in new object?[] { null, "Admin", "Technician", "Client, Admin", "Unknown", 7 })
        {
            using var visitor = NewClient();
            await TokenAsync(visitor);
            var email = $"injected{index++}@tests.invalid";
            var request = Request(email); request.Remove("phoneNumber");
            request["role"] = role; request["roles"] = "Admin, Technician";
            request["verificationStatus"] = "Approved";
            var response = await visitor.PostAsJsonAsync("/api/auth/register", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            AssertSafeAccount(await response.Content.ReadFromJsonAsync<JsonElement>(), email);
            var user = await UserAsync(email);
            Assert.Equal(UserRoles.Client, user.Roles); Assert.Null(user.TechnicianProfile); Assert.Null(user.PhoneNumber);
            Assert.Equal(HttpStatusCode.Forbidden, (await visitor.GetAsync("/api/admin/users")).StatusCode);
        }
        Assert.Equal((11, 0), await CountsAsync());
    }

    [Fact]
    public async Task RegistrationValidation_RejectsInvalidFields_WithoutCreatingAccounts()
    {
        using var visitor = NewClient();
        await TokenAsync(visitor);
        var baseline = await CountsAsync();
        var invalidFields = new (string Field, object? Value)[]
        {
            ("displayName", null), ("displayName", "   "), ("displayName", new string('x', 101)),
            ("email", null), ("email", "not-an-email"), ("email", new string('x', 245) + "@tests.invalid"),
            ("phoneNumber", new string('1', 21)), ("password", null), ("password", "short7!"),
            ("password", new string('x', 129))
        };
        foreach (var (field, value) in invalidFields)
        {
            var request = Request(); request[field] = value;
            Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        }
        Assert.Equal(baseline, await CountsAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RegistrationRequiresCsrf_AndRejectsCaseInsensitiveExistingOrInactiveEmail()
    {
        using var visitor = NewClient();
        var baseline = await CountsAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/auth/register", Request())).StatusCode);
        visitor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid-test-token");
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/auth/register", Request())).StatusCode);
        await TokenAsync(visitor);
        foreach (var email in new[] { "  ADMIN@TESTS.INVALID  ", "  INACTIVE@TESTS.INVALID  " })
            Assert.Equal(HttpStatusCode.Conflict, (await visitor.PostAsJsonAsync("/api/auth/register", Request(email))).StatusCode);
        Assert.Equal(baseline, await CountsAsync());
        Assert.Equal(UserStatus.Inactive, (await UserAsync("inactive@tests.invalid")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task SignedInRegistration_ReturnsConflict_AndPreservesExistingIdentity()
    {
        var baseline = await CountsAsync();
        foreach (var name in new[] { "admin", "client", "technician" })
        {
            using var account = NewClient();
            await LoginAsync(account, name + "@tests.invalid");
            var before = await account.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(HttpStatusCode.Conflict, (await account.PostAsJsonAsync("/api/auth/register", Request("switch@tests.invalid"))).StatusCode);
            var after = await account.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(before.GetProperty("id").GetInt32(), after.GetProperty("id").GetInt32());
            Assert.Equal(before.GetProperty("roles").GetString(), after.GetProperty("roles").GetString());
        }
        Assert.Equal(baseline, await CountsAsync());
    }

    [Fact]
    public async Task AdminCreation_AlwaysCreatesClient_WithoutSwitchingAdminSession()
    {
        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetInt32();
        foreach (var injectedRole in new[] { "Client", "Technician", "Admin" })
        {
            var email = "created" + injectedRole.ToLowerInvariant() + "@tests.invalid";
            var request = Request(email); request["role"] = injectedRole;
            request["roles"] = "Admin"; request["status"] = "Inactive"; request["verificationStatus"] = "Approved";
            var response = await admin.PostAsJsonAsync("/api/admin/users", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            AssertSafeAccount(await response.Content.ReadFromJsonAsync<JsonElement>(), email);
            Assert.Equal(adminId, (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetInt32());
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/users")).StatusCode);
            var user = await UserAsync(email);
            Assert.Equal(UserRoles.Client, user.Roles); Assert.Null(user.TechnicianProfile);
            using var createdClient = NewClient();
            await LoginAsync(createdClient, email);
            AssertSafeAccount(await createdClient.GetFromJsonAsync<JsonElement>("/api/auth/me"), email);
            Assert.Equal(HttpStatusCode.Forbidden, (await createdClient.GetAsync("/api/admin/users")).StatusCode);
        }
        Assert.Equal((8, 0), await CountsAsync());
    }

    [Fact]
    public async Task AdminCreation_ValidatesFieldsAndDuplicates_AndRequiresCsrf()
    {
        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        var baseline = await CountsAsync();
        var invalid = Request(); invalid["password"] = "short7!";
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users", invalid)).StatusCode);
        foreach (var email in new[] { "  CLIENT@TESTS.INVALID  ", "  INACTIVE@TESTS.INVALID  " })
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/users", Request(email))).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users", Request())).StatusCode);
        Assert.Equal(baseline, await CountsAsync());
        AssertSafeAccount(await admin.GetFromJsonAsync<JsonElement>("/api/auth/me"), "admin@tests.invalid", "Admin");
    }

    public void Dispose() => _factory.Dispose();
}
