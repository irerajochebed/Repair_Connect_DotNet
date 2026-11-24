using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Tests.IntegrationTests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestPassword = "Test-Only-Password-814";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _documentsRoot = Path.Combine(Path.GetTempPath(), "RepairConnect-test-documents", Guid.NewGuid().ToString("N"));

    public string DocumentsRoot => _documentsRoot;

    public ApiTestFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["TechnicianDocuments:StoragePath"] = _documentsRoot }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<RepairConnectDbContext>();
            services.RemoveAll<DbContextOptions<RepairConnectDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RepairConnectDbContext>>();
            services.AddSingleton(new DbContextOptionsBuilder<RepairConnectDbContext>().UseSqlite(_connection).Options);
            services.AddScoped<RepairConnectDbContext, SqliteTestDbContext>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RepairConnectDbContext>();
        context.Database.EnsureCreated();
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var (name, role, status) in new[]
        {
            ("admin", UserRoles.Admin, UserStatus.Active),
            ("otheradmin", UserRoles.Admin, UserStatus.Active),
            ("client", UserRoles.Client, UserStatus.Active),
            ("technician", UserRoles.Technician, UserStatus.Active),
            ("inactive", UserRoles.Client, UserStatus.Inactive)
        })
        {
            var user = new ApplicationUser
            {
                Email = name + "@tests.invalid", NormalizedEmail = (name + "@tests.invalid").ToUpperInvariant(),
                DisplayName = name, Roles = role, Status = status, CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, TestPassword);
            context.Users.Add(user);
        }
        context.SaveChanges();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(_documentsRoot)) Directory.Delete(_documentsRoot, recursive: true);
        }
    }
}
