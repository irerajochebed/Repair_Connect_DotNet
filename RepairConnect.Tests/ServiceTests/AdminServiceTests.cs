using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Services;
using RepairConnect.Services.Common;
using Xunit;

namespace RepairConnect.Tests.ServiceTests;

public class AdminServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RepairConnectDbContext> _options;

    public AdminServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<RepairConnectDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new SqliteTestDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private RepairConnectDbContext CreateContext() => new SqliteTestDbContext(_options);

    [Fact]
    public async Task Reject_WithoutReason_Fails()
    {
        // Arrange
        using var context = CreateContext();
        var admin = new ApplicationUser
        {
            DisplayName = "Admin User",
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var techUser = new ApplicationUser
        {
            DisplayName = "Tech User",
            Email = "tech@test.local",
            NormalizedEmail = "TECH@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Technician,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(admin, techUser);
        await context.SaveChangesAsync();

        var profile = new TechnicianProfile
        {
            UserId = techUser.Id,
            Specialization = "Electrical",
            YearsOfExperience = 5,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.TechnicianProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new AdminTechnicianService(context);

        // Act
        var resultEmpty = await service.RejectTechnicianAsync(techUser.Id, "", admin.Id);
        var resultWhitespace = await service.RejectTechnicianAsync(techUser.Id, "   ", admin.Id);

        // Assert
        Assert.False(resultEmpty.Succeeded);
        Assert.Equal(ServiceErrorType.BadRequest, resultEmpty.ErrorType);
        Assert.False(resultWhitespace.Succeeded);
        Assert.Equal(ServiceErrorType.BadRequest, resultWhitespace.ErrorType);
    }

    [Fact]
    public async Task Approve_SetsReviewerFields()
    {
        // Arrange
        using var context = CreateContext();
        var admin = new ApplicationUser
        {
            DisplayName = "Reviewer Admin",
            Email = "reviewer@test.local",
            NormalizedEmail = "REVIEWER@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var techUser = new ApplicationUser
        {
            DisplayName = "Tech User",
            Email = "tech@test.local",
            NormalizedEmail = "TECH@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Technician,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(admin, techUser);
        await context.SaveChangesAsync();

        var profile = new TechnicianProfile
        {
            UserId = techUser.Id,
            Specialization = "Plumbing",
            YearsOfExperience = 3,
            Status = VerificationStatus.Pending,
            RejectionReason = "Previously missing documents",
            CreatedAt = DateTime.UtcNow
        };
        context.TechnicianProfiles.Add(profile);

        var doc = new TechnicianDocument
        {
            TechnicianUserId = techUser.Id,
            Document = "documents/license.pdf",
            UploadedAt = DateTime.UtcNow
        };
        context.TechnicianDocuments.Add(doc);
        await context.SaveChangesAsync();

        var service = new AdminTechnicianService(context);

        // Act
        var result = await service.ApproveTechnicianAsync(techUser.Id, admin.Id);

        // Assert
        Assert.True(result.Succeeded);

        using var verifyContext = CreateContext();
        var updatedProfile = await verifyContext.TechnicianProfiles.FirstAsync(p => p.UserId == techUser.Id);
        Assert.Equal(VerificationStatus.Approved, updatedProfile.Status);
        Assert.Equal(admin.Id, updatedProfile.ReviewedByUserId);
        Assert.NotNull(updatedProfile.ReviewedAt);
        Assert.Null(updatedProfile.RejectionReason);
    }

    [Fact]
    public async Task Approve_WithNoDocuments_Succeeds()
    {
        // Arrange
        using var context = CreateContext();
        var admin = new ApplicationUser
        {
            DisplayName = "Admin User",
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var techUser = new ApplicationUser
        {
            DisplayName = "Tech User",
            Email = "tech@test.local",
            NormalizedEmail = "TECH@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Technician,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(admin, techUser);
        await context.SaveChangesAsync();

        var profile = new TechnicianProfile
        {
            UserId = techUser.Id,
            Specialization = "Carpentry",
            YearsOfExperience = 2,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.TechnicianProfiles.Add(profile);
        await context.SaveChangesAsync();

        var service = new AdminTechnicianService(context);

        // Act
        var result = await service.ApproveTechnicianAsync(techUser.Id, admin.Id);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(VerificationStatus.Approved, profile.Status);
    }

    [Fact]
    public async Task Admin_CannotDeactivateThemselves()
    {
        // Arrange
        using var context = CreateContext();
        var admin = new ApplicationUser
        {
            DisplayName = "Sole Admin",
            Email = "soleadmin@test.local",
            NormalizedEmail = "SOLEADMIN@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var service = new UserAdminService(context, new AccountCreationService(context, passwordHasher));

        // Act
        var result = await service.UpdateUserStatusAsync(admin.Id, UserStatus.Inactive, admin.Id);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
        Assert.Contains("cannot deactivate", result.Error, StringComparison.OrdinalIgnoreCase);

        using var verifyContext = CreateContext();
        var currentAdmin = await verifyContext.Users.FirstAsync(u => u.Id == admin.Id);
        Assert.Equal(UserStatus.Active, currentAdmin.Status);
    }

    [Fact]
    public async Task DashboardCounts_AreCorrect()
    {
        // Arrange
        using var context = CreateContext();

        // 1 Client, 2 Technicians, 1 Admin
        var client = new ApplicationUser
        {
            DisplayName = "Client A",
            Email = "client@test.local",
            NormalizedEmail = "CLIENT@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Client,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var tech1 = new ApplicationUser
        {
            DisplayName = "Tech 1",
            Email = "tech1@test.local",
            NormalizedEmail = "TECH1@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Technician,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var tech2 = new ApplicationUser
        {
            DisplayName = "Tech 2",
            Email = "tech2@test.local",
            NormalizedEmail = "TECH2@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Technician,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var admin = new ApplicationUser
        {
            DisplayName = "Admin",
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            PasswordHash = "hash",
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(client, tech1, tech2, admin);
        await context.SaveChangesAsync();

        // 1 Pending profile, 1 Approved profile
        context.TechnicianProfiles.AddRange(
            new TechnicianProfile
            {
                UserId = tech1.Id,
                Specialization = "Electrical",
                YearsOfExperience = 4,
                Status = VerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            },
            new TechnicianProfile
            {
                UserId = tech2.Id,
                Specialization = "Plumbing",
                YearsOfExperience = 6,
                Status = VerificationStatus.Approved,
                CreatedAt = DateTime.UtcNow
            }
        );

        var category = new ServiceCategory
        {
            Name = "General Repair",
            IsActive = true
        };
        context.ServiceCategories.Add(category);
        await context.SaveChangesAsync();

        // 2 Open requests, 1 Completed request
        var req1 = new RepairRequest
        {
            ClientUserId = client.Id,
            CategoryId = category.Id,
            Title = "Leak",
            Description = "Sink leak",
            ServiceLocation = "Kigali",
            Status = RepairRequestStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        var req2 = new RepairRequest
        {
            ClientUserId = client.Id,
            CategoryId = category.Id,
            Title = "Switch",
            Description = "Broken switch",
            ServiceLocation = "Kigali",
            Status = RepairRequestStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        var req3 = new RepairRequest
        {
            ClientUserId = client.Id,
            CategoryId = category.Id,
            Title = "Oven",
            Description = "Oven broken",
            ServiceLocation = "Kigali",
            Status = RepairRequestStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        context.RepairRequests.AddRange(req1, req2, req3);
        await context.SaveChangesAsync();

        // 1 Completed booking, 1 Pending booking
        context.Bookings.AddRange(
            new Booking
            {
                RepairRequestId = req3.Id,
                TechnicianUserId = tech2.Id,
                Status = BookingStatus.Completed,
                CreatedAt = DateTime.UtcNow
            },
            new Booking
            {
                RepairRequestId = req1.Id,
                TechnicianUserId = tech1.Id,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var service = new AdminDashboardService(context);

        // Act
        var result = await service.GetDashboardMetricsAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);

        var metrics = result.Data;
        Assert.Equal(1, metrics.PendingTechnicians);
        Assert.Equal(1, metrics.TotalClients);
        Assert.Equal(2, metrics.TotalTechnicians);
        Assert.Equal(2, metrics.OpenRequests);
        Assert.Equal(1, metrics.CompletedBookings);
    }
}
