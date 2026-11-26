namespace RepairConnect.Models;

public sealed class TechnicianDocument
{
    public int Id { get; set; }
    public int TechnicianUserId { get; set; }
    public TechnicianProfile Technician { get; set; } = null!;
    public required string Document { get; set; }
    public DateTime UploadedAt { get; set; }
}
