namespace RepairConnect.Models;

public sealed class ServiceCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TechnicianCategory> TechnicianCategories { get; set; } = new List<TechnicianCategory>();
    public ICollection<RepairRequest> RepairRequests { get; set; } = new List<RepairRequest>();
}
