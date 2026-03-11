using Microsoft.AspNetCore.Authorization;
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

    public LostFoundController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
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
    public async Task<IActionResult> Create(string itemDescription, string? location, string? foundBy, string? storageLocation)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(itemDescription))
        {
            TempData["Error"] = "Item description is required.";
            return RedirectToAction("Index");
        }

        var item = new LostFound
        {
            HotelId = hotelId.Value,
            ItemDescription = itemDescription.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Status = "found",
            FoundBy = string.IsNullOrWhiteSpace(foundBy) ? null : foundBy.Trim(),
            StorageLocation = string.IsNullOrWhiteSpace(storageLocation) ? null : storageLocation.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.LostFoundItems.Add(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Lost & Found item recorded.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Claim(int id, string? claimedBy, string? guestName)
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

        item.Status = "claimed";
        item.ClaimedBy = string.IsNullOrWhiteSpace(claimedBy) ? null : claimedBy.Trim();
        item.GuestName = string.IsNullOrWhiteSpace(guestName) ? null : guestName.Trim();
        item.ClaimedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
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
