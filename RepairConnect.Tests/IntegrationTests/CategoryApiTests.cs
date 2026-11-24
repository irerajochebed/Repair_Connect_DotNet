using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepairConnect.Data;
using RepairConnect.Models;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class CategoryApiTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private static async Task TokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
    }

    private static async Task LoginAsync(HttpClient client, string account = "admin")
    {
        await TokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = account + "@tests.invalid", password = ApiTestFactory.TestPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await TokenAsync(client);
    }

    private async Task<int> AddCategoryAsync(string name, bool isActive = true, string? description = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        var category = new ServiceCategory { Name = name, Description = description, IsActive = isActive };
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    [Fact]
    public async Task EmptyCatalog_AndInitializer_DoNotInventServices()
    {
        using var client = NewClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
            await DbInitializer.SeedAsync(db, new ConfigurationBuilder().Build(),
                new PasswordHasher<ApplicationUser>(), NullLogger.Instance);
            Assert.False(await db.ServiceCategories.AnyAsync());
        }
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray());
        var home = await client.GetStringAsync("/");
        Assert.Contains("No services available", home);
        Assert.Matches("<select\\b(?=[^>]*\\bid=\"quick-category\")(?=[^>]*\\bdisabled)[^>]*>", home);
    }

    [Fact]
    public async Task Crud_UpdatesPublicCatalog_AndHomepageDropdownAndTiles()
    {
        using var admin = NewClient();
        using var visitor = NewClient();
        await LoginAsync(admin);
        var created = await admin.PostAsJsonAsync("/api/admin/categories", new
        {
            name = "  Drain cleaning  ", description = "Clear blocked drains.", isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = category.GetProperty("id").GetInt32();
        Assert.Equal("Drain cleaning", category.GetProperty("name").GetString());
        Assert.Equal("Clear blocked drains.", category.GetProperty("description").GetString());
        Assert.True(category.GetProperty("isActive").GetBoolean());

        var fetched = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/categories/{id}");
        Assert.Equal(id, fetched.GetProperty("id").GetInt32());
        var publicItems = await visitor.GetFromJsonAsync<JsonElement>("/api/categories");
        Assert.Equal(id, Assert.Single(publicItems.EnumerateArray()).GetProperty("id").GetInt32());
        var home = await visitor.GetStringAsync("/");
        Assert.Contains($"<option value=\"{id}\">Drain cleaning</option>", home);
        Assert.Contains($"aria-describedby=\"service-hint-{id}\"", home);

        var updated = await admin.PutAsJsonAsync($"/api/admin/categories/{id}", new
        {
            name = "Water system repairs", description = "Repairs to taps and pipework.", isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var renamed = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Water system repairs", renamed.GetProperty("name").GetString());
        home = await visitor.GetStringAsync("/");
        Assert.Contains($"<option value=\"{id}\">Water system repairs</option>", home);
        Assert.Contains("Water system repairs", home);
        Assert.DoesNotContain("Drain cleaning", home);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/categories/{id}", new
        {
            name = "Water system repairs", description = (string?)null, isActive = false
        })).StatusCode);
        Assert.Empty((await visitor.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray());
        home = await visitor.GetStringAsync("/");
        Assert.DoesNotContain("Water system repairs", home);
        Assert.DoesNotContain($"service-hint-{id}", home);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/categories/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/categories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/categories/{id}")).StatusCode);
        home = await visitor.GetStringAsync("/");
        Assert.DoesNotContain("Water system repairs", home);
        Assert.Empty((await visitor.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray());
    }

    [Fact]
    public async Task CatalogAndAdminFilters_ReturnPersistedRecordsWithPagination()
    {
        await AddCategoryAsync("Zulu repairs", description: "Last service");
        await AddCategoryAsync("Alpha repairs", description: "First service");
        await AddCategoryAsync("Hidden repairs", isActive: false);
        using var client = NewClient();
        var publicItems = (await client.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray().ToArray();
        Assert.Equal(new[] { "Alpha repairs", "Zulu repairs" }, publicItems.Select(item => item.GetProperty("name").GetString()));
        await LoginAsync(client);
        var active = await client.GetFromJsonAsync<JsonElement>("/api/admin/categories?search=repairs&isActive=true&page=2&pageSize=1");
        Assert.Equal(2, active.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, active.GetProperty("pageIndex").GetInt32());
        Assert.True(active.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(active.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal("Zulu repairs", Assert.Single(active.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        var inactive = await client.GetFromJsonAsync<JsonElement>("/api/admin/categories?isActive=false&search=Hidden");
        Assert.Equal(1, inactive.GetProperty("totalCount").GetInt32());
        Assert.Equal("Hidden repairs", Assert.Single(inactive.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        foreach (var query in new[] { "page=0", "pageSize=0", "pageSize=51", "page=2147483647&pageSize=50", "isActive=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/categories?" + query)).StatusCode);
    }

    [Fact]
    public async Task AdminRoutes_RequireAdministrator_AndAllWritesRequireCsrf()
    {
        var id = await AddCategoryAsync("Protected repairs");
        var request = new { name = "Unauthorized repairs", description = (string?)null, isActive = true };
        using var visitor = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/admin/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync($"/api/admin/categories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/api/admin/categories", request)).StatusCode);
        using var client = NewClient();
        await LoginAsync(client, "client");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/categories", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/admin/categories/{id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/admin/categories/{id}")).StatusCode);

        using var admin = NewClient();
        await LoginAsync(admin);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/categories", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/categories/{id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/admin/categories/{id}")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>().ServiceCategories.SingleAsync();
        Assert.Equal("Protected repairs", persisted.Name);
    }

    [Fact]
    public async Task ValidationAndDuplicateNames_RejectInvalidData_WithoutChangingStoredCategory()
    {
        var id = await AddCategoryAsync("Pipe repairs");
        var otherId = await AddCategoryAsync("Wiring repairs");
        using var admin = NewClient();
        await LoginAsync(admin);
        foreach (var invalid in new object[]
        {
            new { name = "   ", description = (string?)null, isActive = true },
            new { name = new string('x', 101), description = (string?)null, isActive = true },
            new { name = "Valid name", description = new string('x', 1001), isActive = true },
            new { name = "Valid name" },
            new { name = "Valid name", description = (string?)null, isActive = (bool?)null }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/categories", invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/categories/{id}", invalid)).StatusCode);
        }
        var duplicate = new { name = "  PIPE REPAIRS  ", description = (string?)null, isActive = true };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/categories", duplicate)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/categories/{otherId}", duplicate)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/categories/{id}", new
        {
            name = "Pipe repairs", description = "Updated without renaming", isActive = true
        })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        Assert.Equal(2, await db.ServiceCategories.CountAsync());
        Assert.Equal("Wiring repairs", (await db.ServiceCategories.FindAsync(otherId))!.Name);
    }

    [Fact]
    public async Task MissingCategories_Return404_ForReadUpdateAndDelete()
    {
        using var admin = NewClient();
        await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/categories/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/categories/999999", new
        {
            name = "Missing category", description = (string?)null, isActive = true
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/categories/999999")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LinkedCategories_CannotBeDeleted_ButCanBeDeactivated(bool linkedToRequest)
    {
        var id = await AddCategoryAsync("Linked repairs");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
            if (linkedToRequest)
            {
                db.RepairRequests.Add(new RepairRequest
                {
                    ClientUserId = await db.Users.Where(user => user.Email == "client@tests.invalid").Select(user => user.Id).SingleAsync(),
                    CategoryId = id, Title = "Leaking tap", Description = "Repair the tap", ServiceLocation = "Test location"
                });
            }
            else
            {
                var technicianId = await db.Users.Where(user => user.Email == "technician@tests.invalid").Select(user => user.Id).SingleAsync();
                db.TechnicianProfiles.Add(new TechnicianProfile { UserId = technicianId, Specialization = "Repairs", YearsOfExperience = 1 });
                db.TechnicianCategories.Add(new TechnicianCategory { TechnicianUserId = technicianId, CategoryId = id });
            }
            await db.SaveChangesAsync();
        }
        using var admin = NewClient();
        await LoginAsync(admin);
        var denied = await admin.DeleteAsync($"/api/admin/categories/{id}");
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Contains("deactiv", (await denied.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/categories/{id}", new
        {
            name = "Linked repairs", description = (string?)null, isActive = false
        })).StatusCode);
        using var visitor = NewClient();
        Assert.Empty((await visitor.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray());
        using var verificationScope = _factory.Services.CreateScope();
        var verifyDb = verificationScope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        Assert.False((await verifyDb.ServiceCategories.FindAsync(id))!.IsActive);
        Assert.Equal(1, linkedToRequest ? await verifyDb.RepairRequests.CountAsync() : await verifyDb.TechnicianCategories.CountAsync());
    }

    public void Dispose() => _factory.Dispose();
}
