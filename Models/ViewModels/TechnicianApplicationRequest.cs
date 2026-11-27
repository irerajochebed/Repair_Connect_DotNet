using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public sealed class TechnicianApplicationRequest
{
    private string? _specialization;
    private string? _bio;
    private string? _address;

    [Required, MaxLength(100)]
    public string? Specialization { get => _specialization; set => _specialization = value?.Trim(); }

    [Required, Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }
    public int[] CategoryIds { get; set; } = [];

    [Required, MaxLength(2000)]
    public string? Bio { get => _bio; set => _bio = value?.Trim(); }

    [Required, MaxLength(300)]
    public string? Address { get => _address; set => _address = value?.Trim(); }

    public IFormFile? Document { get; set; }
}
