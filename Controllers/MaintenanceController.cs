using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("Maintenance")]
public class MaintenanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public MaintenanceController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string status = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var allItems = await _db.PreventiveMaintenances
            .Where(p => p.HotelId == hotelId.Value)
            .OrderBy(p => p.NextDue)
            .ToListAsync();

        var today = DateTime.Today;

        // Counts
        var scheduledCount = allItems.Count(p => p.Status == "scheduled" && p.NextDue >= today);
        var inProgressCount = allItems.Count(p => p.Status == "in-progress");
        var completedCount = allItems.Count(p => p.Status == "completed");
        var overdueCount = allItems.Count(p => p.NextDue < today && p.Status != "completed");

        var filteredItems = status switch
        {
            "scheduled" => allItems.Where(p => p.Status == "scheduled" && p.NextDue >= today).ToList(),
            "in-progress" => allItems.Where(p => p.Status == "in-progress").ToList(),
            "completed" => allItems.Where(p => p.Status == "completed").ToList(),
            "overdue" => allItems.Where(p => p.NextDue < today && p.Status != "completed").ToList(),
            _ => allItems
        };

        ViewBag.Items = filteredItems;
        ViewBag.Status = status;
        ViewBag.Today = today;
        ViewBag.TotalCount = allItems.Count;
        ViewBag.ScheduledCount = scheduledCount;
        ViewBag.InProgressCount = inProgressCount;
        ViewBag.CompletedCount = completedCount;
        ViewBag.OverdueCount = overdueCount;

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description, string frequency, string? assignedTo, DateTime nextDue)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var item = new PreventiveMaintenance
        {
            HotelId = hotelId.Value,
            Title = title,
            Description = description,
            Frequency = frequency,
            AssignedTo = assignedTo,
            NextDue = nextDue,
            Status = "scheduled",
            CreatedAt = DateTime.UtcNow
        };

        _db.PreventiveMaintenances.Add(item);
        await _db.SaveChangesAsync();

        TempData["Success"] = "PM item created.";
        return RedirectToAction("Index");
    }

    [HttpPost("Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var item = await _db.PreventiveMaintenances
            .FirstOrDefaultAsync(p => p.Id == id && p.HotelId == hotelId.Value);

        if (item != null)
        {
            item.Status = "completed";
            item.LastCompleted = DateTime.UtcNow;

            // Calculate new NextDue based on frequency
            item.NextDue = item.Frequency switch
            {
                "daily" => DateTime.Today.AddDays(1),
                "weekly" => DateTime.Today.AddDays(7),
                "monthly" => DateTime.Today.AddMonths(1),
                "quarterly" => DateTime.Today.AddMonths(3),
                "yearly" => DateTime.Today.AddYears(1),
                _ => DateTime.Today.AddMonths(1)
            };

            await _db.SaveChangesAsync();
            TempData["Success"] = "PM item marked as completed.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var item = await _db.PreventiveMaintenances
            .FirstOrDefaultAsync(p => p.Id == id && p.HotelId == hotelId.Value);

        if (item != null)
        {
            _db.PreventiveMaintenances.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = "PM item deleted.";
        }

        return RedirectToAction("Index");
    }
}
