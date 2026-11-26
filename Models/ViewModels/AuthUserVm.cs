using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public class AuthUserVm
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRoles Roles { get; set; }
    public UserStatus Status { get; set; }
}
