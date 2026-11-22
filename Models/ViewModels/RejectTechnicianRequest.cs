using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public class RejectTechnicianRequest
{
    [Required]
    public required string Reason { get; set; }
}
