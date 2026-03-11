using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("DailyChecks")]
public class DailyChecksController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public DailyChecksController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var allChecks = await _db.DailyChecks
            .Where(c => c.HotelId == hotelId.Value && c.Date == today)
            .OrderBy(c => c.Category)
            .ThenBy(c => c.CheckItem)
            .ToListAsync();

        var filteredChecks = filter switch
        {
            "pending" => allChecks.Where(c => !c.IsCompleted).ToList(),
            "completed" => allChecks.Where(c => c.IsCompleted).ToList(),
            _ => allChecks
        };

        var totalCount = allChecks.Count;
        var completedCount = allChecks.Count(c => c.IsCompleted);
        var pendingCount = totalCount - completedCount;
        var progressPercent = totalCount > 0 ? (int)Math.Round((double)completedCount / totalCount * 100) : 0;

        ViewBag.Checks = filteredChecks;
        ViewBag.AllChecks = allChecks;
        ViewBag.Filter = filter;
        ViewBag.Today = today;
        ViewBag.TotalCount = totalCount;
        ViewBag.CompletedCount = completedCount;
        ViewBag.PendingCount = pendingCount;
        ViewBag.ProgressPercent = progressPercent;

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string checkItem, string category)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var check = new DailyCheck
        {
            HotelId = hotelId.Value,
            CheckItem = checkItem,
            Category = category,
            Date = DateTime.Today.ToString("yyyy-MM-dd"),
            CreatedAt = DateTime.UtcNow
        };

        _db.DailyChecks.Add(check);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Check item added.";
        return RedirectToAction("Index");
    }

    [HttpPost("Toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, string? completedBy)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var check = await _db.DailyChecks
            .FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);

        if (check != null)
        {
            check.IsCompleted = !check.IsCompleted;
            check.CompletedBy = check.IsCompleted ? completedBy : null;
            await _db.SaveChangesAsync();

            // Check if all daily checks are now completed
            if (check.IsCompleted)
            {
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var allChecks = await _db.DailyChecks
                    .Where(c => c.HotelId == hotelId.Value && c.Date == today)
                    .ToListAsync();

                if (allChecks.Count > 0 && allChecks.All(c => c.IsCompleted))
                {
                    // Notify GMs that all daily checks are completed
                    var user = await _userManager.GetUserAsync(User);
                    var gmUserIds = await _db.UserHotelRoles
                        .Where(r => r.HotelId == hotelId.Value &&
                                    (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
                        .Select(r => r.UserId)
                        .Distinct()
                        .ToListAsync();

                    foreach (var gmId in gmUserIds)
                    {
                        if (gmId == user?.Id) continue;
                        await _notifications.CreateAsync(
                            hotelId.Value,
                            gmId,
                            "Daily Checks Completed",
                            $"All {allChecks.Count} checks for today have been completed.",
                            "/DailyChecks",
                            "success");
                    }
                }
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost("AddNote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var check = await _db.DailyChecks
            .FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);

        if (check != null)
        {
            check.Notes = notes;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Note updated.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var check = await _db.DailyChecks
            .FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);

        if (check != null)
        {
            _db.DailyChecks.Remove(check);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Check item deleted.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("SeedDefaults")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedDefaults()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var defaults = new List<(string Item, string Category)>
        {
            // Pool (4)
            ("Check pool water temperature", "pool"),
            ("Test chlorine levels", "pool"),
            ("Test pH levels", "pool"),
            ("Inspect pool area cleanliness", "pool"),
            // Lobby (4)
            ("Inspect lobby furniture condition", "lobby"),
            ("Check lobby lighting", "lobby"),
            ("Verify lobby temperature", "lobby"),
            ("Check lobby restrooms", "lobby"),
            // Elevator (3)
            ("Test all elevator buttons", "elevator"),
            ("Check elevator emergency phone", "elevator"),
            ("Inspect elevator doors operation", "elevator"),
            // Security (3)
            ("Review security camera footage", "security"),
            ("Check all emergency exits", "security"),
            ("Verify key card system", "security"),
            // Fire (3)
            ("Test fire alarm panel", "fire"),
            ("Inspect fire extinguishers", "fire"),
            ("Check emergency lighting", "fire"),
            // Parking (3)
            ("Inspect parking lot lighting", "parking"),
            ("Check parking gate operation", "parking"),
            ("Review parking lot cleanliness", "parking"),
            // Housekeeping (4)
            ("Verify linen inventory count", "housekeeping"),
            ("Check housekeeping supply levels", "housekeeping"),
            ("Inspect random guest rooms", "housekeeping"),
            ("Review housekeeping cart setup", "housekeeping"),
            // General (3)
            ("Check HVAC system operation", "general"),
            ("Inspect common area cleanliness", "general"),
            ("Review maintenance log entries", "general"),
        };

        foreach (var (item, category) in defaults)
        {
            _db.DailyChecks.Add(new DailyCheck
            {
                HotelId = hotelId.Value,
                CheckItem = item,
                Category = category,
                Date = today,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "27 default checks created for today.";
        return RedirectToAction("Index");
    }
}
