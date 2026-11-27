using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class RepairWorkflowTests
{
    [Fact]
    public async Task ClientTracksTechniciansJobAndReviewsItOnce()
    {
        using var factory = new ApiTestFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        var service = new ServiceCategory { Name = "Refrigeration", IsActive = true };
        var technician = await db.Users.SingleAsync(u => u.DisplayName == "technician");
        technician.Roles |= UserRoles.Client;
        db.TechnicianCategories.Add(new TechnicianCategory
        {
            Category = service,
            Technician = new TechnicianProfile { User = technician, Specialization = "Cooling", Status = VerificationStatus.Approved }
        });
        await db.SaveChangesAsync();

        using var owner = factory.CreateClient();
        using var worker = factory.CreateClient();
        Assert.Contains("Refrigeration", await owner.GetStringAsync("/Explore"));
        await Login(owner, "client");
        await Login(worker, "technician");

        var created = await owner.PostAsJsonAsync("/api/repair-requests", new {
            categoryId = service.Id, title = "Fridge stopped cooling", description = "Fan still runs",
            serviceLocation = "Kigali", urgency = "High"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var path = $"/api/repair-requests/{id}";
        Assert.Contains("Fridge stopped cooling", await worker.GetStringAsync("/Dashboard"));
        var schedule = new { scheduledDate = "2027-01-10" };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path + "/accept", schedule)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await worker.PostAsJsonAsync(path + "/accept", schedule)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await worker.PostAsJsonAsync(path + "/accept", schedule)).StatusCode);
        Assert.Contains("Accepted", await owner.GetStringAsync("/Dashboard"));
        Assert.Equal(HttpStatusCode.Conflict, (await worker.PostAsync(path + "/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await worker.PostAsync(path + "/start", null)).StatusCode);
        Assert.Contains("In progress", await owner.GetStringAsync("/Dashboard"));
        Assert.Equal(HttpStatusCode.NoContent, (await worker.PostAsync(path + "/complete", null)).StatusCode);
        var review = new { rating = 5, comment = "Cooling again" };
        Assert.Equal(HttpStatusCode.NotFound, (await worker.PostAsJsonAsync(path + "/review", review)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync(path + "/review", review)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/review", review)).StatusCode);
        Assert.Contains("Cooling again", await owner.GetStringAsync("/Dashboard"));
    }

    [Fact]
    public async Task MultipleServicesCreateOneRequestPerDistinctActiveCategory()
    {
        using var factory = new ApiTestFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        var categories = new[] { new ServiceCategory { Name = "Doors", IsActive = true },
            new ServiceCategory { Name = "Windows", IsActive = true } };
        db.ServiceCategories.AddRange(categories);
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();
        await Login(client, "client");
        Task<HttpResponseMessage> Submit(params int[] ids) => client.PostAsJsonAsync("/api/repair-requests",
            new { categoryIds = ids, title = "Frame repair", description = "Damaged frames", serviceLocation = "Kigali", urgency = "Medium" });
        var first = categories[0].Id;
        var second = categories[1].Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(first, -1)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Submit(first, second, first)).StatusCode);
        Assert.Equal(new[] { first, second }, await db.RepairRequests.Where(r => r.Title == "Frame repair")
            .OrderBy(r => r.CategoryId).Select(r => r.CategoryId).ToArrayAsync());
    }

    private static async Task Login(HttpClient client, string name)
    {
        async Task SetToken()
        {
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        }

        await SetToken();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = name + "@tests.invalid", password = ApiTestFactory.TestPassword });
        response.EnsureSuccessStatusCode();
        await SetToken();
    }
}
