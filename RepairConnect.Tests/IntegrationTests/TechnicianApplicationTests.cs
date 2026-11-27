using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class TechnicianApplicationTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<< /Type /Catalog >>endobj\ntrailer<<>>\n%%EOF\n");
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
    private static async Task LoginAsync(HttpClient client, string email)
    {
        await TokenAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiTestFactory.TestPassword })).StatusCode);
        await TokenAsync(client);
    }
    private async Task<int> CategoryAsync(string name = "Electrical repairs", bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        var category = new ServiceCategory { Name = name, IsActive = isActive };
        db.ServiceCategories.Add(category); await db.SaveChangesAsync();
        return category.Id;
    }
    private static MultipartFormDataContent Application(int categoryId,
        Dictionary<string, string?>? overrides = null, byte[]? bytes = null,
        string fileName = "certificate.pdf", string contentType = "application/pdf", bool includeDocument = true)
    {
        var fields = new Dictionary<string, string?>
        {
            ["categoryId"] = categoryId.ToString(), ["specialization"] = "Electrical installation",
            ["yearsOfExperience"] = "3", ["bio"] = "Home repair experience", ["address"] = "Kigali"
        };
        foreach (var (key, value) in overrides ?? []) fields[key] = value;
        var content = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
            if (value != null) content.Add(new StringContent(value), key);
        if (includeDocument)
        {
            var document = new ByteArrayContent(bytes ?? Pdf);
            document.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(document, "document", fileName);
        }
        return content;
    }
    private async Task<(int Profiles, int Documents, int Links)> CountsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        return (await db.TechnicianProfiles.CountAsync(), await db.TechnicianDocuments.CountAsync(), await db.TechnicianCategories.CountAsync());
    }
    private int StoredFileCount() => Directory.Exists(_factory.DocumentsRoot)
        ? Directory.GetFiles(_factory.DocumentsRoot, "*", SearchOption.AllDirectories).Length : 0;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdditionalCategoryReviewKeepsOriginalServiceBookable(bool approve)
    {
        var carpentry = await CategoryAsync("Carpentry");
        var plumbing = await CategoryAsync("Plumbing");
        using var tech = NewClient();
        using var admin = NewClient();
        await LoginAsync(tech, "client@tests.invalid");
        await LoginAsync(admin, "admin@tests.invalid");
        var id = (await tech.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetInt32();
        using var original = Application(carpentry, includeDocument: false);
        (await tech.PostAsync("/api/technician-application", original)).EnsureSuccessStatusCode();
        var path = $"/api/admin/technicians/{id}";
        (await admin.PutAsync(path + "/approve", null)).EnsureSuccessStatusCode();
        await LoginAsync(tech, "client@tests.invalid");
        (await tech.PostAsJsonAsync("/api/technician-application/categories", new { categoryIds = new[] { plumbing } })).EnsureSuccessStatusCode();

        using var customer = NewClient();
        await TokenAsync(customer);
        (await customer.PostAsJsonAsync("/api/auth/register", new { displayName = "Owner", email = "owner@tests.invalid",
            password = ApiTestFactory.TestPassword })).EnsureSuccessStatusCode();
        await TokenAsync(customer);
        var response = await customer.PostAsJsonAsync("/api/repair-requests", new { categoryIds = new[] { carpentry, plumbing },
            title = "Kitchen repairs", description = "Leaking tap and broken cabinet", serviceLocation = "Kigali", urgency = "Medium" });
        var jobs = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        async Task<HttpStatusCode> Accept(int index) =>
            (await tech.PostAsJsonAsync($"/api/repair-requests/{jobs[index]}/accept", new { })).StatusCode;
        Assert.Equal(HttpStatusCode.NoContent, await Accept(0));
        Assert.Equal(HttpStatusCode.Conflict, await Accept(1));
        var review = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(plumbing, Assert.Single(review.GetProperty("pendingCategories").EnumerateArray()).GetProperty("id").GetInt32());
        (await admin.PutAsJsonAsync(path + "/category-review", new { categoryIds = new[] { plumbing }, approve, reason = "Training required" })).EnsureSuccessStatusCode();
        Assert.Equal(approve ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, await Accept(1));
        var profile = await tech.GetFromJsonAsync<JsonElement>("/api/technician-application");
        var categories = profile.GetProperty("categories").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32(), x => x.GetProperty("status").GetString());
        Assert.Equal("Approved", categories[carpentry]);
        Assert.Equal(approve ? "Approved" : "Rejected", categories[plumbing]);
    }

    [Fact]
    public async Task ApprovalEnablesRepairsInBothRequestedCategories()
    {
        var carpentry = await CategoryAsync("Carpentry");
        var plumbing = await CategoryAsync("Plumbing");
        using var applicant = NewClient();
        using var admin = NewClient();
        await LoginAsync(applicant, "client@tests.invalid");
        await LoginAsync(admin, "admin@tests.invalid");
        var account = await applicant.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var id = account.GetProperty("id").GetInt32();
        using var application = Application(carpentry, new() { ["categoryId"] = null }, includeDocument: false);
        foreach (var category in new[] { carpentry, plumbing, plumbing })
            application.Add(new StringContent(category.ToString()), "categoryIds");
        Assert.Equal(HttpStatusCode.Created, (await applicant.PostAsync("/api/technician-application", application)).StatusCode);
        var review = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/technicians/{id}");
        Assert.Equal(2, review.GetProperty("categories").GetArrayLength());

        using var customer = NewClient();
        await TokenAsync(customer);
        (await customer.PostAsJsonAsync("/api/auth/register", new { displayName = "Homeowner",
            email = "homeowner@tests.invalid", password = ApiTestFactory.TestPassword })).EnsureSuccessStatusCode();
        await TokenAsync(customer);
        var response = await customer.PostAsJsonAsync("/api/repair-requests", new { categoryIds = new[] { carpentry, plumbing },
            title = "Kitchen repairs", description = "Fix cabinet and tap", serviceLocation = "Kigali", urgency = "Medium" });
        response.EnsureSuccessStatusCode();
        var requests = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = requests.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsJsonAsync($"/api/repair-requests/{ids[0]}/accept", new { })).StatusCode);
        (await admin.PutAsync($"/api/admin/technicians/{id}/approve", null)).EnsureSuccessStatusCode();
        await LoginAsync(applicant, "client@tests.invalid");
        foreach (var request in ids)
            Assert.Equal(HttpStatusCode.NoContent, (await applicant.PostAsJsonAsync($"/api/repair-requests/{request}/accept", new { })).StatusCode);
    }

    [Fact]
    public async Task SignupApplyReviewApprove_PreservesAccountAndClientAccess_AndRequiresFreshLoginForTechnicianRole()
    {
        var categoryId = await CategoryAsync();
        using var owner = NewClient();
        await TokenAsync(owner);
        var signup = await owner.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Repair Applicant", email = "applicant@tests.invalid", password = ApiTestFactory.TestPassword,
            role = "Admin", roles = "Admin, Technician"
        });
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        var identity = await signup.Content.ReadFromJsonAsync<JsonElement>();
        var userId = identity.GetProperty("id").GetInt32();
        Assert.Equal("Client", identity.GetProperty("roles").GetString());
        Assert.Equal((0, 0, 0), await CountsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/technician-application")).StatusCode);
        await TokenAsync(owner);
        using (var application = Application(categoryId, new()
        {
            ["userId"] = "1", ["roles"] = "Admin, Technician", ["status"] = "Approved",
            ["availabilityStatus"] = "Available", ["reviewedByUserId"] = "1"
        }))
            Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync("/api/technician-application", application)).StatusCode);
        var pending = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        Assert.Equal(userId, pending.GetProperty("userId").GetInt32());
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal("Offline", pending.GetProperty("availabilityStatus").GetString());
        Assert.Equal(categoryId, Assert.Single(pending.GetProperty("categories").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal("Client", (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").GetString());
        Assert.Equal((1, 1, 1), await CountsAsync());
        using (var duplicate = Application(categoryId))
            Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/technician-application", duplicate)).StatusCode);
        Assert.Equal(1, StoredFileCount());

        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/admin/technicians?status=Pending&search=Repair%20Applicant");
        Assert.Equal(userId, Assert.Single(directory.GetProperty("items").EnumerateArray()).GetProperty("userId").GetInt32());
        var review = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/technicians/{userId}");
        Assert.Single(review.GetProperty("documents").EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/admin/technicians/{userId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(owner, "applicant@tests.invalid");
        var approvedIdentity = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(userId, approvedIdentity.GetProperty("id").GetInt32());
        Assert.Equal("Client, Technician", approvedIdentity.GetProperty("roles").GetString());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/Dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/admin/users")).StatusCode);
        var approved = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        using (var resubmit = Application(categoryId))
            Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/technician-application", resubmit)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.Equal(UserRoles.Client | UserRoles.Technician, (await db.Users.FindAsync(userId))!.Roles);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/technicians/{userId}/reject", new { reason = "Updated review requires new evidence." })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/auth/me")).StatusCode);
        await LoginAsync(owner, "applicant@tests.invalid");
        var clientIdentity = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(userId, clientIdentity.GetProperty("id").GetInt32());
        Assert.Equal("Client", clientIdentity.GetProperty("roles").GetString());
        var revoked = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        Assert.Equal("Rejected", revoked.GetProperty("status").GetString());
        Assert.Equal("Offline", revoked.GetProperty("availabilityStatus").GetString());
    }

    [Fact]
    public async Task SupportingDocuments_ArePrivateAttachments_ForOwnerAndAdministratorOnly()
    {
        var categoryId = await CategoryAsync();
        using var owner = NewClient();
        await LoginAsync(owner, "client@tests.invalid");
        using (var content = Application(categoryId))
            Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync("/api/technician-application", content)).StatusCode);
        var ownProfile = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        var document = Assert.Single(ownProfile.GetProperty("documents").EnumerateArray());
        var url = document.GetProperty("downloadUrl").GetString()!;
        Assert.Equal($"/api/technician-application/documents/{document.GetProperty("id").GetInt32()}", url);
        Assert.DoesNotContain(_factory.DocumentsRoot, ownProfile.GetRawText());
        Assert.DoesNotContain("App_Data", ownProfile.GetRawText());
        var download = await owner.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(download.Headers.CacheControl?.NoStore);
        using var visitor = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(url)).StatusCode);
        using var other = NewClient();
        await TokenAsync(other);
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Other Client", email = "otherclient@tests.invalid", password = ApiTestFactory.TestPassword
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/technician-application?userId=" + ownProfile.GetProperty("userId").GetInt32())).StatusCode);
        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        Assert.Equal(Pdf, await (await admin.GetAsync(url)).Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/technician-application/documents/999999")).StatusCode);
    }

    [Fact]
    public async Task RejectedApplicant_CanResubmitOnSameAccount_AndRemainsClientUntilApproval()
    {
        var firstCategory = await CategoryAsync();
        var secondCategory = await CategoryAsync("Plumbing repairs");
        using var owner = NewClient();
        await LoginAsync(owner, "client@tests.invalid");
        var id = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetInt32();
        using (var first = Application(firstCategory))
            Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync("/api/technician-application", first)).StatusCode);
        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/technicians/{id}/reject", new { reason = "Please upload a clearer certificate." })).StatusCode);
        var rejected = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal("Please upload a clearer certificate.", rejected.GetProperty("rejectionReason").GetString());
        Assert.Equal("Client", (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").GetString());
        using (var updated = Application(secondCategory, new() { ["specialization"] = "Plumbing", ["yearsOfExperience"] = "5" }))
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/technician-application", updated)).StatusCode);
        var resubmitted = await owner.GetFromJsonAsync<JsonElement>("/api/technician-application");
        Assert.Equal(id, resubmitted.GetProperty("userId").GetInt32());
        Assert.Equal("Pending", resubmitted.GetProperty("status").GetString());
        Assert.Equal("Plumbing", resubmitted.GetProperty("specialization").GetString());
        Assert.Equal(5, resubmitted.GetProperty("yearsOfExperience").GetInt32());
        Assert.Equal(JsonValueKind.Null, resubmitted.GetProperty("rejectionReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, resubmitted.GetProperty("reviewedAt").ValueKind);
        Assert.Equal(secondCategory, Assert.Single(resubmitted.GetProperty("categories").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal((1, 1, 1), await CountsAsync());
        Assert.Equal(1, StoredFileCount());
        Assert.Equal("Client", (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").GetString());
    }

    [Fact]
    public async Task ApplicationRoutes_RequireClientAndCsrf_WithoutCreatingPartialApplications()
    {
        var categoryId = await CategoryAsync();
        using var visitor = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/technician-application")).StatusCode);
        using (var content = Application(categoryId))
            Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/api/technician-application", content)).StatusCode);
        using var admin = NewClient();
        await LoginAsync(admin, "admin@tests.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/technician-application")).StatusCode);
        using (var content = Application(categoryId))
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync("/api/technician-application", content)).StatusCode);
        using var owner = NewClient();
        await LoginAsync(owner, "client@tests.invalid");
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using (var content = Application(categoryId))
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/technician-application", content)).StatusCode);
        Assert.Equal((0, 0, 0), await CountsAsync()); Assert.Equal(0, StoredFileCount());
    }

    [Fact]
    public async Task ApplicationValidation_RejectsInvalidCategoryFieldsFilesAndOversize_WithoutSavingAnything()
    {
        var categoryId = await CategoryAsync();
        var inactiveId = await CategoryAsync("Inactive service", isActive: false);
        using var owner = NewClient();
        await LoginAsync(owner, "client@tests.invalid");
        foreach (var (field, value) in new (string, string?)[]
        {
            ("categoryId", null), ("categoryId", "0"), ("categoryId", "999999"), ("categoryId", inactiveId.ToString()),
            ("address", null), ("address", "  "), ("bio", null), ("bio", "  "),
            ("specialization", null), ("specialization", "  "), ("specialization", new string('x', 101)),
            ("yearsOfExperience", null), ("yearsOfExperience", "-1"), ("yearsOfExperience", "61"), ("yearsOfExperience", "invalid"),
            ("bio", new string('x', 2001)), ("address", new string('x', 301))
        })
        {
            using var invalid = Application(categoryId, new() { [field] = value });
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/technician-application", invalid)).StatusCode);
        }
        using (var invalidType = Application(categoryId, fileName: "program.exe", contentType: "application/octet-stream"))
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/technician-application", invalidType)).StatusCode);
        using (var invalidMagic = Application(categoryId, bytes: Encoding.ASCII.GetBytes("This is not a PDF file.")))
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/technician-application", invalidMagic)).StatusCode);
        var oversized = new byte[5 * 1024 * 1024 + 1]; Pdf.CopyTo(oversized, 0);
        using (var tooLarge = Application(categoryId, bytes: oversized))
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/technician-application", tooLarge)).StatusCode);
        Assert.Equal((0, 0, 0), await CountsAsync()); Assert.Equal(0, StoredFileCount());
        Assert.Equal("Client", (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").GetString());
        using (var missingFile = Application(categoryId, includeDocument: false))
            Assert.Equal(HttpStatusCode.Created, (await owner.PostAsync("/api/technician-application", missingFile)).StatusCode);
        Assert.Equal((1, 0, 1), await CountsAsync());
    }

    [Fact]
    public async Task FailedApplicationProfileInsert_RollsBackProfileDocumentLinksAndPrivateFile_KeepingClientAccount()
    {
        var categoryId = await CategoryAsync();
        using var owner = NewClient();
        await LoginAsync(owner, "client@tests.invalid");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectProfileInsert BEFORE INSERT ON TechnicianProfiles BEGIN SELECT RAISE(ABORT, 'Test profile failure'); END;");
        }
        using var application = Application(categoryId);
        var response = await owner.PostAsync("/api/technician-application", application);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Test profile failure", await response.Content.ReadAsStringAsync());
        Assert.Equal((0, 0, 0), await CountsAsync()); Assert.Equal(0, StoredFileCount());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/technician-application")).StatusCode);
        Assert.Equal("Client", (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").GetString());
    }

    public void Dispose() => _factory.Dispose();
}
