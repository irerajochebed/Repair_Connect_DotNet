using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(
        RepairConnectDbContext context,
        IConfiguration configuration,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger logger)
    {
        // 1. Seed Categories
        var categoryNames = new[]
        {
            "Phone Repair",
            "Computer Repair",
            "Electrical",
            "Plumbing",
            "Home Appliances",
            "Electronics"
        };

        foreach (var catName in categoryNames)
        {
            if (!await context.ServiceCategories.AnyAsync(c => c.Name == catName))
            {
                context.ServiceCategories.Add(new ServiceCategory
                {
                    Name = catName,
                    Description = $"{catName} services",
                    IsActive = true
                });
            }
        }
        await context.SaveChangesAsync();

        // 2. Seed Admin from configuration / user-secrets
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        var adminDisplayName = configuration["SeedAdmin:DisplayName"];

        ApplicationUser? adminUser = null;

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword) ||
            string.IsNullOrWhiteSpace(adminDisplayName))
        {
            logger.LogWarning(
                "Seed admin configuration is missing (SeedAdmin:Email, SeedAdmin:Password, SeedAdmin:DisplayName). Skipping initial admin seeding.");
        }
        else
        {
            var normalizedAdminEmail = adminEmail.Trim().ToUpperInvariant();
            adminUser = await context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedAdminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    DisplayName = adminDisplayName.Trim(),
                    Email = adminEmail.Trim(),
                    NormalizedEmail = normalizedAdminEmail,
                    Roles = UserRoles.Admin,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };
                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, adminPassword);

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();
                logger.LogInformation("Initial administrator account seeded successfully: {Email}", adminEmail);
            }
        }

        // 3. Seed Demo Data (Development only when SeedDemoData == true)
        var seedDemo = configuration.GetValue<bool>("SeedDemoData");
        if (!seedDemo)
        {
            return;
        }

        // Existing admin fallback for references
        if (adminUser == null)
        {
            adminUser = await context.Users.FirstOrDefaultAsync(u => (u.Roles & UserRoles.Admin) == UserRoles.Admin);
        }

        var categories = await context.ServiceCategories.ToDictionaryAsync(c => c.Name, c => c);

        // Seed 1 Client
        var clientEmail = "client@repairconnect.local";
        var normalizedClientEmail = clientEmail.ToUpperInvariant();
        var clientUser = await context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedClientEmail);

        if (clientUser == null)
        {
            clientUser = new ApplicationUser
            {
                DisplayName = "Alice Client",
                Email = clientEmail,
                NormalizedEmail = normalizedClientEmail,
                PhoneNumber = "+250788100200",
                Roles = UserRoles.Client,
                Status = UserStatus.Active,
                Address = "Kigali, Gasabo",
                CreatedAt = DateTime.UtcNow
            };
            clientUser.PasswordHash = passwordHasher.HashPassword(clientUser, "Client123!");
            context.Users.Add(clientUser);
            await context.SaveChangesAsync();
        }

        // Technician 1 (Pending, 2 documents)
        var tech1Email = "tech1@repairconnect.local";
        var normTech1 = tech1Email.ToUpperInvariant();
        if (!await context.Users.AnyAsync(u => u.NormalizedEmail == normTech1))
        {
            var tech1 = new ApplicationUser
            {
                DisplayName = "Bob Builder",
                Email = tech1Email,
                NormalizedEmail = normTech1,
                PhoneNumber = "+250788111222",
                Roles = UserRoles.Technician,
                Status = UserStatus.Active,
                Address = "Kigali, Kicukiro",
                CreatedAt = DateTime.UtcNow
            };
            tech1.PasswordHash = passwordHasher.HashPassword(tech1, "Tech123!");
            context.Users.Add(tech1);
            await context.SaveChangesAsync();

            var profile1 = new TechnicianProfile
            {
                UserId = tech1.Id,
                Specialization = "Phone Repair",
                YearsOfExperience = 4,
                Bio = "Certified smartphone and tablet repair technician.",
                VerificationStatus = VerificationStatus.Pending,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedAt = DateTime.UtcNow
            };
            context.TechnicianProfiles.Add(profile1);
            await context.SaveChangesAsync();

            context.TechnicianDocuments.AddRange(
                new TechnicianDocument
                {
                    TechnicianUserId = tech1.Id,
                    Document = "uploads/documents/bob_national_id.pdf",
                    UploadedAt = DateTime.UtcNow
                },
                new TechnicianDocument
                {
                    TechnicianUserId = tech1.Id,
                    Document = "uploads/documents/bob_tech_certification.pdf",
                    UploadedAt = DateTime.UtcNow
                }
            );

            if (categories.TryGetValue("Phone Repair", out var catPhone))
            {
                context.TechnicianCategories.Add(new TechnicianCategory
                {
                    TechnicianUserId = tech1.Id,
                    CategoryId = catPhone.Id
                });
            }
            if (categories.TryGetValue("Electronics", out var catElec))
            {
                context.TechnicianCategories.Add(new TechnicianCategory
                {
                    TechnicianUserId = tech1.Id,
                    CategoryId = catElec.Id
                });
            }
            await context.SaveChangesAsync();
        }

        // Technician 2 (Pending, 1 document)
        var tech2Email = "tech2@repairconnect.local";
        var normTech2 = tech2Email.ToUpperInvariant();
        if (!await context.Users.AnyAsync(u => u.NormalizedEmail == normTech2))
        {
            var tech2 = new ApplicationUser
            {
                DisplayName = "Charlie Plumber",
                Email = tech2Email,
                NormalizedEmail = normTech2,
                PhoneNumber = "+250788333444",
                Roles = UserRoles.Technician,
                Status = UserStatus.Active,
                Address = "Kigali, Nyarugenge",
                CreatedAt = DateTime.UtcNow
            };
            tech2.PasswordHash = passwordHasher.HashPassword(tech2, "Tech123!");
            context.Users.Add(tech2);
            await context.SaveChangesAsync();

            var profile2 = new TechnicianProfile
            {
                UserId = tech2.Id,
                Specialization = "Plumbing",
                YearsOfExperience = 6,
                Bio = "Experienced licensed plumber for household leakages and pipe fittings.",
                VerificationStatus = VerificationStatus.Pending,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedAt = DateTime.UtcNow
            };
            context.TechnicianProfiles.Add(profile2);
            await context.SaveChangesAsync();

            context.TechnicianDocuments.Add(new TechnicianDocument
            {
                TechnicianUserId = tech2.Id,
                Document = "uploads/documents/charlie_plumbing_license.pdf",
                UploadedAt = DateTime.UtcNow
            });

            if (categories.TryGetValue("Plumbing", out var catPlumb))
            {
                context.TechnicianCategories.Add(new TechnicianCategory
                {
                    TechnicianUserId = tech2.Id,
                    CategoryId = catPlumb.Id
                });
            }
            await context.SaveChangesAsync();
        }

        // Technician 3 (Approved, 1 document)
        var tech3Email = "tech3@repairconnect.local";
        var normTech3 = tech3Email.ToUpperInvariant();
        if (!await context.Users.AnyAsync(u => u.NormalizedEmail == normTech3))
        {
            var tech3 = new ApplicationUser
            {
                DisplayName = "Diana Electric",
                Email = tech3Email,
                NormalizedEmail = normTech3,
                PhoneNumber = "+250788555666",
                Roles = UserRoles.Technician,
                Status = UserStatus.Active,
                Address = "Kigali, Gasabo",
                CreatedAt = DateTime.UtcNow
            };
            tech3.PasswordHash = passwordHasher.HashPassword(tech3, "Tech123!");
            context.Users.Add(tech3);
            await context.SaveChangesAsync();

            var profile3 = new TechnicianProfile
            {
                UserId = tech3.Id,
                Specialization = "Electrical",
                YearsOfExperience = 8,
                Bio = "Certified master electrician.",
                VerificationStatus = VerificationStatus.Approved,
                ReviewedByUserId = adminUser?.Id,
                ReviewedAt = DateTime.UtcNow,
                AvailabilityStatus = AvailabilityStatus.Available,
                CreatedAt = DateTime.UtcNow
            };
            context.TechnicianProfiles.Add(profile3);
            await context.SaveChangesAsync();

            context.TechnicianDocuments.Add(new TechnicianDocument
            {
                TechnicianUserId = tech3.Id,
                Document = "uploads/documents/diana_master_license.pdf",
                UploadedAt = DateTime.UtcNow
            });

            if (categories.TryGetValue("Electrical", out var catElec))
            {
                context.TechnicianCategories.Add(new TechnicianCategory
                {
                    TechnicianUserId = tech3.Id,
                    CategoryId = catElec.Id
                });
            }
            await context.SaveChangesAsync();

            // Demo Open Request & Completed Booking for metrics
            if (categories.TryGetValue("Electrical", out var catForReq))
            {
                var openReq = new RepairRequest
                {
                    ClientUserId = clientUser.Id,
                    CategoryId = catForReq.Id,
                    Title = "Circuit Breaker Tripping",
                    Description = "Main kitchen circuit trips when microwave runs.",
                    ServiceLocation = "Kigali, Gasabo, House 42",
                    Urgency = RepairUrgency.High,
                    Status = RepairRequestStatus.Open,
                    CreatedAt = DateTime.UtcNow
                };
                context.RepairRequests.Add(openReq);

                var completedReq = new RepairRequest
                {
                    ClientUserId = clientUser.Id,
                    CategoryId = catForReq.Id,
                    Title = "Water Heater Installation",
                    Description = "Installed 50L electric water heater.",
                    ServiceLocation = "Kigali, Gasabo, House 42",
                    Urgency = RepairUrgency.Medium,
                    Status = RepairRequestStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                context.RepairRequests.Add(completedReq);
                await context.SaveChangesAsync();

                var booking = new Booking
                {
                    RepairRequestId = completedReq.Id,
                    TechnicianUserId = tech3.Id,
                    Status = BookingStatus.Completed,
                    ScheduledDate = DateTime.UtcNow.AddDays(-2),
                    AssignedAt = DateTime.UtcNow.AddDays(-2),
                    CompletedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                context.Bookings.Add(booking);
                await context.SaveChangesAsync();
            }
        }
    }
}
