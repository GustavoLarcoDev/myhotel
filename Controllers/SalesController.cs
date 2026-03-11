using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class SalesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;

    public SalesController(
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

    public async Task<IActionResult> Index()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var allSales = await _db.GroupSales
            .Where(gs => gs.HotelId == hotelId.Value)
            .OrderByDescending(gs => gs.CreatedAt)
            .ToListAsync();

        ViewBag.TotalSales = allSales.Count;
        ViewBag.TotalRevenue = allSales.Where(gs => gs.Status != "cancelled").Sum(gs => gs.Revenue);
        ViewBag.PendingCount = allSales.Count(gs => gs.Status == "tentative");
        ViewBag.ConfirmedCount = allSales.Count(gs => gs.Status == "confirmed");

        ViewBag.RecentSales = allSales.Take(10).ToList();

        return View();
    }

    public async Task<IActionResult> GroupSales(string status = "all", string search = "")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var allSales = await _db.GroupSales
            .Where(gs => gs.HotelId == hotelId.Value)
            .OrderByDescending(gs => gs.CreatedAt)
            .ToListAsync();

        var filtered = allSales.AsEnumerable();

        if (status != "all")
        {
            filtered = filtered.Where(gs => gs.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            filtered = filtered.Where(gs =>
                gs.GroupName.ToLower().Contains(s) ||
                (gs.ContactName != null && gs.ContactName.ToLower().Contains(s)) ||
                (gs.ContactEmail != null && gs.ContactEmail.ToLower().Contains(s)));
        }

        ViewBag.TotalCount = allSales.Count;
        ViewBag.TentativeCount = allSales.Count(gs => gs.Status == "tentative");
        ViewBag.ConfirmedCount = allSales.Count(gs => gs.Status == "confirmed");
        ViewBag.CancelledCount = allSales.Count(gs => gs.Status == "cancelled");
        ViewBag.CompletedCount = allSales.Count(gs => gs.Status == "completed");
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSearch = search ?? "";

        return View(filtered.ToList());
    }

    [HttpGet]
    public IActionResult CreateGroupSale()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroupSale(string groupName, string? contactName, string? contactEmail,
        string? contactPhone, DateTime checkIn, DateTime checkOut, int roomsBlocked, decimal revenue,
        string status, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(groupName))
        {
            TempData["Error"] = "Group name is required.";
            return View();
        }

        var user = await _userManager.GetUserAsync(User);

        var groupSale = new GroupSale
        {
            HotelId = hotelId.Value,
            GroupName = groupName.Trim(),
            ContactName = contactName?.Trim(),
            ContactEmail = contactEmail?.Trim(),
            ContactPhone = contactPhone?.Trim(),
            CheckIn = checkIn,
            CheckOut = checkOut,
            RoomsBlocked = roomsBlocked,
            Revenue = revenue,
            Status = string.IsNullOrWhiteSpace(status) ? "tentative" : status.Trim(),
            Notes = notes?.Trim(),
            CreatedBy = user?.FullName ?? "System",
            CreatedAt = DateTime.UtcNow
        };

        _db.GroupSales.Add(groupSale);
        await _db.SaveChangesAsync();

        // Notify GMs
        var gmUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value &&
                (r.Role == AppRole.GeneralManager || r.Role == AppRole.Admin))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var gmId in gmUserIds)
        {
            if (gmId == user?.Id) continue;
            await _notifications.CreateAsync(
                hotelId.Value,
                gmId,
                $"New Group Sale: {groupSale.GroupName}",
                $"{groupSale.RoomsBlocked} rooms blocked, ${groupSale.Revenue:N0} revenue",
                "/Sales/GroupSales",
                "info");
        }

        TempData["Success"] = "Group sale created.";
        return RedirectToAction("GroupSales");
    }

    [HttpGet]
    public async Task<IActionResult> EditGroupSale(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var groupSale = await _db.GroupSales.FirstOrDefaultAsync(gs => gs.Id == id && gs.HotelId == hotelId.Value);
        if (groupSale == null)
        {
            TempData["Error"] = "Group sale not found.";
            return RedirectToAction("GroupSales");
        }

        return View(groupSale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroupSale(int id, string groupName, string? contactName, string? contactEmail,
        string? contactPhone, DateTime checkIn, DateTime checkOut, int roomsBlocked, decimal revenue,
        string status, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var groupSale = await _db.GroupSales.FirstOrDefaultAsync(gs => gs.Id == id && gs.HotelId == hotelId.Value);
        if (groupSale == null)
        {
            TempData["Error"] = "Group sale not found.";
            return RedirectToAction("GroupSales");
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            TempData["Error"] = "Group name is required.";
            return View(groupSale);
        }

        var oldStatus = groupSale.Status;

        groupSale.GroupName = groupName.Trim();
        groupSale.ContactName = contactName?.Trim();
        groupSale.ContactEmail = contactEmail?.Trim();
        groupSale.ContactPhone = contactPhone?.Trim();
        groupSale.CheckIn = checkIn;
        groupSale.CheckOut = checkOut;
        groupSale.RoomsBlocked = roomsBlocked;
        groupSale.Revenue = revenue;
        groupSale.Status = string.IsNullOrWhiteSpace(status) ? groupSale.Status : status.Trim();
        groupSale.Notes = notes?.Trim();

        await _db.SaveChangesAsync();

        // Notify GMs on status change
        if (oldStatus != groupSale.Status)
        {
            var user = await _userManager.GetUserAsync(User);
            var gmUserIds = await _db.UserHotelRoles
                .Where(r => r.HotelId == hotelId.Value &&
                    (r.Role == AppRole.GeneralManager || r.Role == AppRole.Admin))
                .Select(r => r.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var gmId in gmUserIds)
            {
                if (gmId == user?.Id) continue;
                await _notifications.CreateAsync(
                    hotelId.Value,
                    gmId,
                    $"Group Sale Updated: {groupSale.GroupName}",
                    $"Status changed from {oldStatus} to {groupSale.Status}",
                    "/Sales/GroupSales",
                    "info");
            }
        }

        TempData["Success"] = "Group sale updated.";
        return RedirectToAction("GroupSales");
    }

    public async Task<IActionResult> Staff()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Find the Sales department for this hotel
        var salesDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value &&
                (d.Name == "Sales" || d.Name == "Sales & Marketing"));

        if (salesDept == null)
        {
            ViewBag.StaffList = new List<object>();
            ViewBag.DepartmentName = "Sales";
            return View();
        }

        ViewBag.DepartmentName = salesDept.Name;

        // Get users in the Sales department
        var userDepts = await _db.UserDepartments
            .Include(ud => ud.User)
            .Where(ud => ud.DepartmentId == salesDept.Id)
            .ToListAsync();

        var userIds = userDepts.Select(ud => ud.UserId).Distinct().ToList();

        var userRoles = await _db.UserHotelRoles
            .Where(ur => ur.HotelId == hotelId.Value && userIds.Contains(ur.UserId))
            .ToListAsync();

        var staffList = userDepts
            .GroupBy(ud => ud.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                var role = userRoles.Where(ur => ur.UserId == user.Id).OrderBy(ur => ur.Role).FirstOrDefault();
                return new
                {
                    User = user,
                    Role = role?.Role ?? AppRole.Employee
                };
            })
            .OrderBy(s => s.Role)
            .ThenBy(s => s.User.FullName)
            .ToList();

        ViewBag.StaffList = staffList;

        return View();
    }
}
