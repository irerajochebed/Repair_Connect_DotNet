using System.ComponentModel.DataAnnotations;
using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public class UpdateUserStatusRequest
{
    [Required]
    [EnumDataType(typeof(UserStatus))]
    public UserStatus? Status { get; set; }
}
