namespace RepairConnect.Models.ViewModels;

public class AdminDashboardVm
{
    public int PendingTechnicians { get; set; }
    public int TotalClients { get; set; }
    public int TotalTechnicians { get; set; }
    public int OpenRequests { get; set; }
    public int CompletedBookings { get; set; }
}
