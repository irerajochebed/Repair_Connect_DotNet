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

public sealed class BookingApiTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

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

    private static async Task LoginAsync(HttpClient client, string account)
    {
        await TokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = account + "@tests.invalid", password = ApiTestFactory.TestPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await TokenAsync(client);
    }

    // Makes the seeded "technician" account an approved plumber; returns (categoryId, technicianId).
    private async Task<(int CategoryId, int TechnicianId)> SeedTechnicianAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        var tech = await db.Users.SingleAsync(u => u.DisplayName == "technician");
        var category = new ServiceCategory { Name = "Plumbing" };
        db.ServiceCategories.Add(category);
        db.TechnicianProfiles.Add(new TechnicianProfile
        {
            UserId = tech.Id, Specialization = "Pipes", YearsOfExperience = 2,
            VerificationStatus = VerificationStatus.Approved, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.TechnicianCategories.Add(new TechnicianCategory { TechnicianUserId = tech.Id, CategoryId = category.Id });
        await db.SaveChangesAsync();
        return (category.Id, tech.Id);
    }

    [Fact]
    public async Task Directory_IsPublic()
    {
        var (_, techId) = await SeedTechnicianAsync();
        using var client = NewClient();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/technicians");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        var profile = await client.GetAsync($"/api/technicians/{techId}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/technicians/99999")).StatusCode);
    }

    [Fact]
    public async Task Requests_RequireSignIn()
    {
        using var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requests")).StatusCode);
    }

    [Fact]
    public async Task Client_CreatesRequest_CancelsBooking_AndCannotReviewUnfinishedWork()
    {
        var (categoryId, techId) = await SeedTechnicianAsync();
        using var client = NewClient();
        await LoginAsync(client, "client");

        var created = await client.PostAsJsonAsync("/api/requests", new
        {
            categoryId, technicianUserId = techId, title = "Burst pipe", description = "Water everywhere",
            serviceLocation = "Kigali", urgency = "High"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var request = await created.Content.ReadFromJsonAsync<JsonElement>();
        var bookingId = request.GetProperty("bookings")[0].GetProperty("id").GetInt32();
        Assert.Equal("Pending", request.GetProperty("bookings")[0].GetProperty("status").GetString());

        var review = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/review", new { rating = 5 });
        Assert.Equal(HttpStatusCode.Conflict, review.StatusCode);

        var cancel = await client.PutAsync($"/api/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/requests");
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Technician_CannotBookThemself()
    {
        var (categoryId, techId) = await SeedTechnicianAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            // Technicians keep their Client role, so they can also make requests.
            var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
            var tech = await db.Users.SingleAsync(u => u.Id == techId);
            tech.Roles = UserRoles.Client | UserRoles.Technician;
            await db.SaveChangesAsync();
        }
        using var client = NewClient();
        await LoginAsync(client, "technician");

        var response = await client.PostAsJsonAsync("/api/requests", new
        {
            categoryId, technicianUserId = techId, title = "Self", description = "Self", serviceLocation = "Home"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
