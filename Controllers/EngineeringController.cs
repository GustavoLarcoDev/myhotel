using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class EngineeringController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;

    public EngineeringController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext,
        NotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
        _notifications = notifications;
    }

    // GET: /Engineering
    public async Task<IActionResult> Index()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var today = DateTime.UtcNow.Date;

        // Stats
        ViewBag.OpenWorkOrders = await _db.WorkOrders
            .CountAsync(w => w.HotelId == hotelId.Value && w.Status != "completed" && w.Status != "cancelled");

        ViewBag.OverduePMs = await _db.PreventiveMaintenances
            .CountAsync(pm => pm.HotelId == hotelId.Value && pm.NextDue < today);

        ViewBag.TotalAssets = await _db.Assets
            .CountAsync(a => a.HotelId == hotelId.Value);

        ViewBag.ReadingsToday = await _db.Readings
            .CountAsync(r => r.HotelId == hotelId.Value && r.CreatedAt.Date == today);

        // Recent work orders for activity section
        ViewBag.RecentWorkOrders = await _db.WorkOrders
            .Where(w => w.HotelId == hotelId.Value && w.Status != "completed" && w.Status != "cancelled")
            .OrderByDescending(w => w.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Upcoming PMs
        ViewBag.UpcomingPMs = await _db.PreventiveMaintenances
            .Where(pm => pm.HotelId == hotelId.Value && pm.Status != "completed")
            .OrderBy(pm => pm.NextDue)
            .Take(5)
            .ToListAsync();

        return View();
    }

    // GET: /Engineering/Staff
    public async Task<IActionResult> Staff()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Find engineering-related departments for this hotel
        var engineeringDeptNames = new[] { "maintenance", "engineering", "facilities" };
        var engineeringDepts = await _db.Departments
            .Where(d => d.HotelId == hotelId.Value && engineeringDeptNames.Contains(d.Name.ToLower()))
            .ToListAsync();

        var engineeringDeptIds = engineeringDepts.Select(d => d.Id).ToList();

        // Get users in those departments
        var userDepartments = await _db.UserDepartments
            .Include(ud => ud.User)
            .Include(ud => ud.Department)
            .Where(ud => engineeringDeptIds.Contains(ud.DepartmentId))
            .ToListAsync();

        var userIds = userDepartments.Select(ud => ud.UserId).Distinct().ToList();

        // Get their hotel roles
        var userRoles = await _db.UserHotelRoles
            .Where(ur => ur.HotelId == hotelId.Value && userIds.Contains(ur.UserId))
            .ToListAsync();

        // Build staff list
        var staffList = userDepartments
            .GroupBy(ud => ud.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                var depts = g.Select(ud => ud.Department).ToList();
                var role = userRoles
                    .Where(ur => ur.UserId == user.Id)
                    .OrderBy(ur => ur.Role)
                    .Select(ur => ur.Role)
                    .FirstOrDefault();
                return new
                {
                    User = user,
                    Role = role,
                    Departments = depts
                };
            })
            .OrderBy(s => s.Role)
            .ThenBy(s => s.User.FullName)
            .ToList();

        ViewBag.StaffList = staffList;
        ViewBag.EngineeringDepartments = engineeringDepts;

        return View();
    }
}
