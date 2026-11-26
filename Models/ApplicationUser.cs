using Microsoft.AspNetCore.Identity;
using RepairConnect.Models.Enums;

namespace RepairConnect.Models;

public class ApplicationUser : IdentityUser<int>
{
    public required string DisplayName { get; set; }
    public UserRoles Roles { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }

    public TechnicianProfile? TechnicianProfile { get; set; }
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
    public ICollection<TechnicianProfile> ReviewedProfiles { get; set; } = new List<TechnicianProfile>();
}
