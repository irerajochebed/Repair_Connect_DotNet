namespace RepairConnect.Models;

public sealed class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRoles Roles { get; set; }
    public AccountStatus Status { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Flags]
public enum UserRoles
{
    None = 0,
    Client = 1,
    Technician = 2,
    Admin = 4
}

public enum AccountStatus
{
    Active,
    Inactive
}
