using System.ComponentModel.DataAnnotations;
using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public class UpdateUserStatusRequest
{
    [Required]
    public UserStatus Status { get; set; }
}
