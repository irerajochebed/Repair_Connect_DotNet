using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Pages;

public class DashboardModel(RepairConnectDbContext db) : PageModel
{
    public List<RepairRequest> Mine { get; private set; } = [];
    public List<RepairRequest> Available { get; private set; } = [];
    public HashSet<int> EligibleCategories { get; private set; } = [];
    public string Panel { get; private set; } = "personal";
    public List<Booking> Jobs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Request.Query["new"] == "1")
            return RedirectToPage("/Explore", new { category = Request.Query["category"].ToString(), location = Request.Query["location"].ToString() });
        if (User.IsInRole("Admin")) return Page();
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (Request.Query["panel"] == "technician")
        {
            Panel = "technician";
            return Page();
        }
        if (User.IsInRole("Technician"))
        {
            Panel = Request.Query["panel"].ToString() switch
            {
                "jobs" => "jobs",
                "personal" => "personal",
                _ => Request.Query.ContainsKey("submitted") ? "personal" : "board"
            };
        }
        if (Panel == "board")
        {
            EligibleCategories = (await db.TechnicianCategories.Where(c => c.TechnicianUserId == id &&
                c.ReviewStatus == null && c.Technician.Status == VerificationStatus.Approved).Select(c => c.CategoryId).ToListAsync(ct)).ToHashSet();
            Available = await db.RepairRequests.AsNoTracking().Include(r => r.Category)
                .Where(r => r.Status == RepairRequestStatus.Open && r.ClientUserId != id)
                .OrderByDescending(r => r.Id).ToListAsync(ct);
        }
        else if (Panel == "jobs")
        {
            Jobs = await db.Bookings.AsNoTracking().Include(b => b.RepairRequest)
                .Where(b => b.TechnicianUserId == id).OrderByDescending(b => b.Id).ToListAsync(ct);
        }
        else
        {
            Mine = await db.RepairRequests.AsNoTracking().Where(r => r.ClientUserId == id)
                .Include(r => r.Category).Include(r => r.Bookings).ThenInclude(b => b.Technician).ThenInclude(t => t.User)
                .Include(r => r.Bookings).ThenInclude(b => b.Review).OrderByDescending(r => r.Id).ToListAsync(ct);
        }
        return Page();
    }
}
