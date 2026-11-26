namespace RepairConnect.Models.Enums;

[Flags]
public enum UserRoles
{
    None = 0,
    Client = 1,
    Technician = 2,
    Admin = 4
}
