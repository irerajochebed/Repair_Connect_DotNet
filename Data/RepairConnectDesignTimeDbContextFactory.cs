using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RepairConnect.Data;

// EF CLI schema commands must not run application startup or seed accounts.
public sealed class RepairConnectDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RepairConnectDbContext>
{
    public RepairConnectDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<RepairConnectDesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("RepairConnect")
            ?? "Server=localhost;Database=RepairConnect;Integrated Security=True;TrustServerCertificate=True";
        return new RepairConnectDbContext(new DbContextOptionsBuilder<RepairConnectDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}
