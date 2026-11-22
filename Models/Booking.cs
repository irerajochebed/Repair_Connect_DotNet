namespace RepairConnect.Models;

public sealed class Booking
{
    public int Id { get; set; }
    public int RepairRequestId { get; set; }
    public RepairRequest RepairRequest { get; set; } = null!;
    public int TechnicianUserId { get; set; }
    public TechnicianProfile Technician { get; set; } = null!;
    public BookingStatus Status { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum BookingStatus
{
    Pending,
    Accepted,
    Declined,
    InProgress,
    Completed,
    Cancelled
}
