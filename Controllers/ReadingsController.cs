using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("Readings")]
public class ReadingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReadingsController(ApplicationDbContext db, HotelContextService hotelContext,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string type = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var allReadings = await _db.Readings
            .Where(r => r.HotelId == hotelId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Counts by type
        var typeCounts = allReadings
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var filteredReadings = type != "all" && !string.IsNullOrEmpty(type)
            ? allReadings.Where(r => r.Type == type).ToList()
            : allReadings;

        ViewBag.Readings = filteredReadings;
        ViewBag.AllReadings = allReadings;
        ViewBag.SelectedType = type;
        ViewBag.TypeCounts = typeCounts;
        ViewBag.TotalCount = allReadings.Count;

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string type, decimal value, string unit, string? location, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var user = await _userManager.GetUserAsync(User);

        var reading = new Reading
        {
            HotelId = hotelId.Value,
            Type = type,
            Value = value,
            Unit = unit,
            Location = location,
            RecordedBy = user?.FullName ?? "Unknown",
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.Readings.Add(reading);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Reading recorded.";
        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var reading = await _db.Readings
            .FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);

        if (reading != null)
        {
            _db.Readings.Remove(reading);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Reading deleted.";
        }

        return RedirectToAction("Index");
    }
}
