using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;

    public DashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
    }

    // GET: /Dashboard
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var hotelId = _hotelContext.CurrentHotelId;

        // If no hotel selected, try to auto-select
        if (hotelId == null)
        {
            var firstRole = await _db.UserHotelRoles
                .Where(r => r.UserId == user.Id)
                .FirstOrDefaultAsync();

            if (firstRole != null)
            {
                await _hotelContext.SetCurrentHotel(firstRole.HotelId);
                hotelId = firstRole.HotelId;
            }
        }

        if (hotelId == null)
        {
            ViewBag.NoHotel = true;
            return View();
        }

        var today = DateTime.UtcNow.Date;
        var todayStr = today.ToString("yyyy-MM-dd");

        // Summary stats
        ViewBag.OpenWorkOrders = await _db.WorkOrders
            .CountAsync(w => w.HotelId == hotelId && w.Status != "completed" && w.Status != "cancelled");

        ViewBag.PendingChecks = await _db.DailyChecks
            .CountAsync(c => c.HotelId == hotelId && c.Date == todayStr && !c.IsCompleted);

        ViewBag.TotalChecks = await _db.DailyChecks
            .CountAsync(c => c.HotelId == hotelId && c.Date == todayStr);

        ViewBag.CompletedChecks = await _db.DailyChecks
            .CountAsync(c => c.HotelId == hotelId && c.Date == todayStr && c.IsCompleted);

        ViewBag.LogsToday = await _db.Logs
            .CountAsync(l => l.HotelId == hotelId && l.CreatedAt.Date == today);

        ViewBag.OpenComplaints = await _db.Complaints
            .CountAsync(c => c.HotelId == hotelId && c.Status != "resolved" && c.Status != "closed");

        ViewBag.UnreadMessages = await _db.Messages
            .CountAsync(m => m.HotelId == hotelId && m.ToUserId == user.Id && !m.IsRead);

        // Recent activity feed - last 10 items combined from work orders, complaints, logs
        var recentWorkOrders = await _db.WorkOrders
            .Where(w => w.HotelId == hotelId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(10)
            .Select(w => new { Type = "Work Order", Title = w.Description, w.Status, w.CreatedAt })
            .ToListAsync();

        var recentComplaints = await _db.Complaints
            .Where(c => c.HotelId == hotelId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new { Type = "Complaint", Title = c.Description, c.Status, c.CreatedAt })
            .ToListAsync();

        var recentLogs = await _db.Logs
            .Where(l => l.HotelId == hotelId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .Select(l => new { Type = "Log", Title = l.Message, Status = l.Category, l.CreatedAt })
            .ToListAsync();

        var activityFeed = recentWorkOrders
            .Concat(recentComplaints)
            .Concat(recentLogs)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToList();

        ViewBag.ActivityFeed = activityFeed;

        return View();
    }

    // GET: /Dashboard/SwitchHotel?hotelId=5
    public async Task<IActionResult> SwitchHotel(int hotelId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        // Verify user has access to this hotel
        var hasAccess = await _db.UserHotelRoles
            .AnyAsync(r => r.UserId == user.Id && r.HotelId == hotelId);

        // Also allow admins
        if (!hasAccess)
        {
            hasAccess = await _db.UserHotelRoles
                .AnyAsync(r => r.UserId == user.Id && r.Role == AppRole.Admin);
        }

        if (!hasAccess)
        {
            TempData["Error"] = "You do not have access to that hotel.";
            return RedirectToAction("Index");
        }

        await _hotelContext.SetCurrentHotel(hotelId);
        TempData["Success"] = "Hotel switched successfully.";
        return RedirectToAction("Index");
    }
}
