using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;

namespace RepairConnect.Tests;

// Portable relational tests; production retains its SQL Server schema and migration history.
public sealed class SqliteTestDbContext(DbContextOptions<RepairConnectDbContext> options)
    : RepairConnectDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var constraint in entity.GetCheckConstraints().Select(check => (check.Name, check.Sql)).ToArray())
            {
                if (constraint.Name is not { } name) continue;
                entity.RemoveCheckConstraint(name);
                entity.AddCheckConstraint(name, constraint.Sql.Replace("N'", "'"));
            }
            foreach (var index in entity.GetIndexes())
                if (index.GetFilter() is { } filter) index.SetFilter(filter.Replace("N'", "'"));
            foreach (var property in entity.GetProperties())
                if (property.GetDefaultValueSql() == "SYSUTCDATETIME()") property.SetDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
