namespace RepairConnect.Models.ViewModels;

public class DocumentVm
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime UploadedAt { get; set; }
}
