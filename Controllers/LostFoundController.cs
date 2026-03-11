using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class LostFoundController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public LostFoundController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string status = "all", string? search = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var allItems = await _db.LostFoundItems
            .Where(i => i.HotelId == hotelId.Value)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // Stats
        ViewBag.TotalCount = allItems.Count;
        ViewBag.FoundCount = allItems.Count(i => i.Status == "found");
        ViewBag.ClaimedCount = allItems.Count(i => i.Status == "claimed");
        ViewBag.DisposedCount = allItems.Count(i => i.Status == "disposed");

        // 90-day warning
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var oldFoundItems = allItems.Where(i => i.Status == "found" && i.CreatedAt <= ninetyDaysAgo).ToList();
        ViewBag.OldFoundCount = oldFoundItems.Count;

        // Filter by status
        var filtered = status switch
        {
            "found" => allItems.Where(i => i.Status == "found").ToList(),
            "claimed" => allItems.Where(i => i.Status == "claimed").ToList(),
            "disposed" => allItems.Where(i => i.Status == "disposed").ToList(),
            _ => allItems
        };

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            filtered = filtered.Where(i =>
                i.ItemDescription.ToLower().Contains(s) ||
                (i.Location != null && i.Location.ToLower().Contains(s)) ||
                (i.FoundBy != null && i.FoundBy.ToLower().Contains(s)) ||
                (i.GuestName != null && i.GuestName.ToLower().Contains(s)) ||
                (i.StorageLocation != null && i.StorageLocation.ToLower().Contains(s))
            ).ToList();
        }

        ViewBag.CurrentStatus = status;
        ViewBag.Search = search;

        return View(filtered);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string itemDescription, string? location, string? storageLocation)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(itemDescription))
        {
            TempData["Error"] = "Item description is required.";
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);

        var item = new LostFound
        {
            HotelId = hotelId.Value,
            ItemDescription = itemDescription.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Status = "found",
            FoundBy = user?.FullName ?? "Unknown",
            StorageLocation = string.IsNullOrWhiteSpace(storageLocation) ? null : storageLocation.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.LostFoundItems.Add(item);
        await _db.SaveChangesAsync();
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
                $"Lost Item Found: {item.ItemDescription}",
                item.Location != null ? $"Found at: {item.Location}" : null,
                "/LostFound",
                "info");
        }

        TempData["Success"] = "Lost & Found item recorded.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Claim(int id, string? guestName)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.LostFoundItems
            .FirstOrDefaultAsync(i => i.Id == id && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);
        item.Status = "claimed";
        item.ClaimedBy = user?.FullName ?? "Unknown";
        item.GuestName = string.IsNullOrWhiteSpace(guestName) ? null : guestName.Trim();
        item.ClaimedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Notify Front Desk department about the claimed item
        var fdDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value &&
                d.Name.ToLower().Contains("front desk"));
        if (fdDept != null)
        {
            await _notifications.NotifyDepartmentAsync(
                hotelId.Value, fdDept.Id,
                $"Item Claimed: {item.ItemDescription}",
                item.GuestName != null ? $"Claimed by: {item.GuestName}" : null,
                "/LostFound",
                "info",
                user?.Id);
        }

        TempData["Success"] = "Item marked as claimed.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Dispose(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.LostFoundItems
            .FirstOrDefaultAsync(i => i.Id == id && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index");
        }

        item.Status = "disposed";
        await _db.SaveChangesAsync();
        TempData["Success"] = "Item marked as disposed.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.LostFoundItems
            .FirstOrDefaultAsync(i => i.Id == id && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index");
        }

        _db.LostFoundItems.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Item deleted.";
        return RedirectToAction("Index");
    }
}
