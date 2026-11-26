using Microsoft.EntityFrameworkCore;
using RepairConnect.Models;

namespace RepairConnect.Data;

public class RepairConnectDbContext : DbContext
{
    public RepairConnectDbContext(DbContextOptions<RepairConnectDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<TechnicianProfile> TechnicianProfiles => Set<TechnicianProfile>();
    public DbSet<TechnicianDocument> TechnicianDocuments => Set<TechnicianDocument>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<TechnicianCategory> TechnicianCategories => Set<TechnicianCategory>();
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RepairConnectDbContext).Assembly);
    }
}
