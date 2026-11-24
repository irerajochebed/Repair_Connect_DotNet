using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly RepairConnectDbContext _context;

    public AdminDashboardService(RepairConnectDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<AdminDashboardVm>> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var pendingTechnicians = await _context.TechnicianProfiles
            .CountAsync(tp => tp.VerificationStatus == VerificationStatus.Pending, cancellationToken);

        var totalClients = await _context.Users
            .CountAsync(u => (u.Roles & UserRoles.Client) == UserRoles.Client, cancellationToken);

        var totalTechnicians = await _context.Users
            .CountAsync(u => (u.Roles & UserRoles.Technician) == UserRoles.Technician, cancellationToken);

        var openRequests = await _context.RepairRequests
            .CountAsync(rr => rr.Status == RepairRequestStatus.Open, cancellationToken);

        var completedBookings = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Completed, cancellationToken);

        var vm = new AdminDashboardVm
        {
            PendingTechnicians = pendingTechnicians,
            TotalClients = totalClients,
            TotalTechnicians = totalTechnicians,
            OpenRequests = openRequests,
            CompletedBookings = completedBookings
        };

        return ServiceResult<AdminDashboardVm>.Success(vm);
    }
}
