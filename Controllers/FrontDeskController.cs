using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class FrontDeskController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;

    public FrontDeskController(
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

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var today = todayStart.ToString("yyyy-MM-dd");

        // Today's pass logs
        var passLogsToday = await _db.PassLogs
            .CountAsync(p => p.HotelId == hotelId.Value && p.CreatedAt >= todayStart && p.CreatedAt < todayEnd);

        // Open complaints
        var openComplaints = await _db.Complaints
            .CountAsync(c => c.HotelId == hotelId.Value && c.Status == "open");

        // Active lost items (not claimed/disposed)
        var activeLostItems = await _db.LostFoundItems
            .CountAsync(l => l.HotelId == hotelId.Value && l.Status != "claimed" && l.Status != "disposed");

        // Today's cash reports
        var todayCashReports = await _db.CashReports
            .CountAsync(cr => cr.HotelId == hotelId.Value && cr.Date == today);

        ViewBag.PassLogsToday = passLogsToday;
        ViewBag.OpenComplaints = openComplaints;
        ViewBag.ActiveLostItems = activeLostItems;
        ViewBag.TodayCashReports = todayCashReports;

        return View();
    }

    public async Task<IActionResult> CashReports(string date = "")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var query = _db.CashReports
            .Where(cr => cr.HotelId == hotelId.Value);

        if (!string.IsNullOrWhiteSpace(date))
        {
            query = query.Where(cr => cr.Date == date);
        }

        var reports = await query
            .OrderByDescending(cr => cr.Date)
            .ThenByDescending(cr => cr.CreatedAt)
            .ToListAsync();

        ViewBag.CurrentDate = date;

        return View(reports);
    }

    [HttpGet]
    public async Task<IActionResult> CreateCashReport()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        ViewBag.TodayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Get the last cash log so the next person can see it
        var lastReport = await _db.CashReports
            .Where(cr => cr.HotelId == hotelId.Value)
            .OrderByDescending(cr => cr.CreatedAt)
            .FirstOrDefaultAsync();
        ViewBag.LastReport = lastReport;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCashReport(string date, string shift,
        int bills100, int bills50, int bills20, int bills10, int bills5, int bills1,
        int coins25, int coins10, int coins5, int coins1,
        int rolls25, int rolls10, int rolls5, int rolls1,
        string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(shift))
        {
            TempData["Error"] = "Date and shift are required.";
            ViewBag.TodayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            return View();
        }

        var user = await _userManager.GetUserAsync(User);

        // Calculate total from bills, coins, and rolls
        decimal cashTotal = (bills100 * 100m) + (bills50 * 50m) + (bills20 * 20m) +
                           (bills10 * 10m) + (bills5 * 5m) + (bills1 * 1m) +
                           (coins25 * 0.25m) + (coins10 * 0.10m) + (coins5 * 0.05m) + (coins1 * 0.01m) +
                           (rolls25 * 10m) + (rolls10 * 5m) + (rolls5 * 2m) + (rolls1 * 0.50m);

        // Get previous report to calculate variance
        var lastReport = await _db.CashReports
            .Where(cr => cr.HotelId == hotelId.Value)
            .OrderByDescending(cr => cr.CreatedAt)
            .FirstOrDefaultAsync();

        var variance = lastReport != null ? cashTotal - lastReport.CashTotal : 0m;

        var report = new CashReport
        {
            HotelId = hotelId.Value,
            Date = date.Trim(),
            Shift = shift.Trim(),
            Bills100 = bills100,
            Bills50 = bills50,
            Bills20 = bills20,
            Bills10 = bills10,
            Bills5 = bills5,
            Bills1 = bills1,
            Coins25 = coins25,
            Coins10 = coins10,
            Coins5 = coins5,
            Coins1 = coins1,
            Rolls25 = rolls25,
            Rolls10 = rolls10,
            Rolls5 = rolls5,
            Rolls1 = rolls1,
            CashTotal = cashTotal,
            OpeningBalance = lastReport?.CashTotal ?? 0m,
            ClosingBalance = cashTotal,
            Variance = variance,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedBy = user?.FullName ?? "Unknown",
            CreatedAt = DateTime.UtcNow
        };

        _db.CashReports.Add(report);
        await _db.SaveChangesAsync();

        // Notify GM(s) of this hotel
        var gmUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value &&
                        (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var gmId in gmUserIds)
        {
            await _notifications.CreateAsync(
                hotelId.Value,
                gmId,
                "New Cash Log",
                $"{report.Shift} shift cash log for {report.Date} by {report.CreatedBy}. Total: {report.CashTotal:C2}" +
                    (lastReport != null ? $" | Previous: {lastReport.CashTotal:C2} | Diff: {variance:C2}" : ""),
                "/FrontDesk/CashReports",
                variance < 0 ? "warning" : "info"
            );
        }

        TempData["Success"] = "Cash log created.";
        return RedirectToAction("CashReports");
    }

    [HttpGet]
    public async Task<IActionResult> EditCashReport(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var report = await _db.CashReports
            .FirstOrDefaultAsync(cr => cr.Id == id && cr.HotelId == hotelId.Value);

        if (report == null)
        {
            TempData["Error"] = "Cash report not found.";
            return RedirectToAction("CashReports");
        }

        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCashReport(int id, string date, string shift, decimal openingBalance, decimal closingBalance, decimal cashIn, decimal cashOut, decimal creditCardTotal, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var report = await _db.CashReports
            .FirstOrDefaultAsync(cr => cr.Id == id && cr.HotelId == hotelId.Value);

        if (report == null)
        {
            TempData["Error"] = "Cash report not found.";
            return RedirectToAction("CashReports");
        }

        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(shift))
        {
            TempData["Error"] = "Date and shift are required.";
            return View(report);
        }

        report.Date = date.Trim();
        report.Shift = shift.Trim();
        report.OpeningBalance = openingBalance;
        report.ClosingBalance = closingBalance;
        report.CashIn = cashIn;
        report.CashOut = cashOut;
        report.CreditCardTotal = creditCardTotal;
        report.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        var expectedClosing = openingBalance + cashIn - cashOut;
        report.Variance = closingBalance - expectedClosing;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Cash report updated.";
        return RedirectToAction("CashReports");
    }

    public async Task<IActionResult> Staff()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Find the Front Desk department for this hotel
        var frontDeskDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value && d.Name == "Front Desk");

        if (frontDeskDept == null)
        {
            ViewBag.StaffList = new List<object>();
            return View();
        }

        // Get users in this department
        var userDepartments = await _db.UserDepartments
            .Include(ud => ud.User)
            .Where(ud => ud.DepartmentId == frontDeskDept.Id)
            .ToListAsync();

        var userIds = userDepartments.Select(ud => ud.UserId).Distinct().ToList();

        var userRoles = await _db.UserHotelRoles
            .Where(ur => ur.HotelId == hotelId.Value && userIds.Contains(ur.UserId))
            .ToListAsync();

        var staffList = userDepartments
            .GroupBy(ud => ud.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                var role = userRoles
                    .Where(ur => ur.UserId == user.Id)
                    .OrderBy(ur => ur.Role)
                    .FirstOrDefault()?.Role ?? AppRole.Employee;
                var isManager = g.Any(ud => ud.IsManager);
                return new { User = user, Role = role, IsManager = isManager };
            })
            .OrderBy(s => s.Role)
            .ThenBy(s => s.User.FullName)
            .ToList();

        ViewBag.StaffList = staffList;

        return View();
    }
}
