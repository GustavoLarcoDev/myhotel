using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("PassLogs")]
public class PassLogsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public PassLogsController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? date = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var selectedDate = string.IsNullOrEmpty(date)
            ? DateTime.Today
            : DateTime.TryParse(date, out var parsedDate) ? parsedDate : DateTime.Today;

        var passLogs = await _db.PassLogs
            .Where(p => p.HotelId == hotelId.Value
                && p.CreatedAt.Date == selectedDate.Date)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var unreadCount = passLogs.Count(p => !p.IsRead);

        // Group by shift transition
        var grouped = passLogs
            .GroupBy(p => $"{p.ShiftFrom}-{p.ShiftTo}")
            .ToDictionary(g => g.Key, g => g.ToList());

        ViewBag.PassLogs = passLogs;
        ViewBag.GroupedPassLogs = grouped;
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        ViewBag.UnreadCount = unreadCount;
        ViewBag.TotalCount = passLogs.Count;

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string shiftFrom, string shiftTo, string message, string priority, string createdBy)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var passLog = new PassLog
        {
            HotelId = hotelId.Value,
            ShiftFrom = shiftFrom,
            ShiftTo = shiftTo,
            Message = message,
            Priority = priority,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.PassLogs.Add(passLog);
        await _db.SaveChangesAsync();

        // Notify the hotel about the new pass log for the incoming shift
        var user = await _userManager.GetUserAsync(User);
        await _notifications.NotifyHotelAsync(
            hotelId.Value,
            $"New Pass Log: {shiftFrom} to {shiftTo}",
            priority == "high" || priority == "urgent"
                ? $"[{priority.ToUpper()}] {(message.Length > 50 ? message.Substring(0, 50) + "..." : message)}"
                : message.Length > 50 ? message.Substring(0, 50) + "..." : message,
            "/PassLogs",
            priority == "high" || priority == "urgent" ? "warning" : "info",
            user?.Id);

        TempData["Success"] = "Pass log entry added successfully.";
        return RedirectToAction("Index");
    }

    [HttpPost("MarkAsRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, string readBy)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var passLog = await _db.PassLogs.FirstOrDefaultAsync(p => p.Id == id && p.HotelId == hotelId.Value);
        if (passLog != null)
        {
            passLog.IsRead = true;
            passLog.ReadBy = readBy;
            passLog.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Pass log marked as read.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var passLog = await _db.PassLogs.FirstOrDefaultAsync(p => p.Id == id && p.HotelId == hotelId.Value);
        if (passLog != null)
        {
            _db.PassLogs.Remove(passLog);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Pass log entry deleted.";
        }

        return RedirectToAction("Index");
    }
}
