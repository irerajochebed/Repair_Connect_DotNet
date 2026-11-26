using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public sealed class SaveServiceCategoryRequest
{
    private string? _name;

    [Required]
    [MaxLength(100)]
    public string? Name
    {
        get => _name;
        set => _name = value?.Trim();
    }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public bool? IsActive { get; set; }
}
