using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("Logs")]
public class LogsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public LogsController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string category = "all", string? date = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var selectedDate = string.IsNullOrEmpty(date)
            ? DateTime.Today
            : DateTime.TryParse(date, out var parsedDate) ? parsedDate : DateTime.Today;

        var query = _db.Logs
            .Where(l => l.HotelId == hotelId.Value
                && l.CreatedAt.Date == selectedDate.Date);

        if (category != "all" && !string.IsNullOrEmpty(category))
        {
            query = query.Where(l => l.Category == category);
        }

        var logs = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

        // Get counts per category for the selected date (unfiltered)
        var allLogsForDate = await _db.Logs
            .Where(l => l.HotelId == hotelId.Value && l.CreatedAt.Date == selectedDate.Date)
            .ToListAsync();

        var categoryCounts = allLogsForDate
            .GroupBy(l => l.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalCount = allLogsForDate.Count;
        var unreadCount = allLogsForDate.Count(l => !l.IsRead);

        ViewBag.Logs = logs;
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        ViewBag.CategoryCounts = categoryCounts;
        ViewBag.TotalCount = totalCount;
        ViewBag.UnreadCount = unreadCount;

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string category, string message)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var user = await _userManager.GetUserAsync(User);

        var log = new Log
        {
            HotelId = hotelId.Value,
            Category = category,
            Message = message,
            CreatedBy = user?.FullName ?? "Unknown",
            CreatedAt = DateTime.UtcNow
        };

        _db.Logs.Add(log);
        await _db.SaveChangesAsync();
        await _notifications.NotifyHotelAsync(
            hotelId.Value,
            $"New Log: {category}",
            message.Length > 50 ? message.Substring(0, 50) + "..." : message,
            "/Logs",
            "info",
            user?.Id);

        TempData["Success"] = "Log entry added successfully.";
        return RedirectToAction("Index");
    }

    [HttpPost("MarkAsRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var user = await _userManager.GetUserAsync(User);
        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == id && l.HotelId == hotelId.Value);
        if (log != null)
        {
            log.IsRead = true;
            log.ReadBy = user?.FullName ?? "Unknown";
            log.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Log marked as read.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == id && l.HotelId == hotelId.Value);
        if (log != null)
        {
            _db.Logs.Remove(log);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Log entry deleted.";
        }

        return RedirectToAction("Index");
    }
}
