namespace RepairConnect.Models;

public sealed class TechnicianCategory
{
    public int TechnicianUserId { get; set; }
    public TechnicianProfile Technician { get; set; } = null!;
    public int CategoryId { get; set; }
    public ServiceCategory Category { get; set; } = null!;
}
