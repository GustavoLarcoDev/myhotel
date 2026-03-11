using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("Assets")]
public class AssetsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public AssetsController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string category = "all", string condition = "all", string? search = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var query = _db.Assets.Where(a => a.HotelId == hotelId.Value);

        if (category != "all" && !string.IsNullOrEmpty(category))
        {
            query = query.Where(a => a.Category == category);
        }

        if (condition != "all" && !string.IsNullOrEmpty(condition))
        {
            query = query.Where(a => a.Condition == condition);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(searchLower) ||
                (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(searchLower)) ||
                (a.Location != null && a.Location.ToLower().Contains(searchLower)));
        }

        var assets = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        // Get all assets for counts
        var allAssets = await _db.Assets
            .Where(a => a.HotelId == hotelId.Value)
            .ToListAsync();

        var today = DateTime.Today;
        var warrantyAlertCount = allAssets.Count(a =>
            a.WarrantyExpiry.HasValue &&
            a.WarrantyExpiry.Value >= today &&
            a.WarrantyExpiry.Value <= today.AddDays(30));

        ViewBag.Assets = assets;
        ViewBag.TotalCount = allAssets.Count;
        ViewBag.WarrantyAlertCount = warrantyAlertCount;
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedCondition = condition;
        ViewBag.Search = search ?? "";

        return View();
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? category, string? serialNumber, string? location, string condition, DateTime? warrantyExpiry, DateTime? purchaseDate, decimal? purchaseCost)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var asset = new Asset
        {
            HotelId = hotelId.Value,
            Name = name,
            Category = category,
            SerialNumber = serialNumber,
            Location = location,
            Condition = condition,
            WarrantyExpiry = warrantyExpiry,
            PurchaseDate = purchaseDate,
            PurchaseCost = purchaseCost,
            CreatedAt = DateTime.UtcNow
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Asset added.";
        return RedirectToAction("Index");
    }

    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? category, string? serialNumber, string? location, string condition, DateTime? warrantyExpiry, DateTime? purchaseDate, decimal? purchaseCost)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == id && a.HotelId == hotelId.Value);

        if (asset != null)
        {
            asset.Name = name;
            asset.Category = category;
            asset.SerialNumber = serialNumber;
            asset.Location = location;
            asset.Condition = condition;
            asset.WarrantyExpiry = warrantyExpiry;
            asset.PurchaseDate = purchaseDate;
            asset.PurchaseCost = purchaseCost;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Asset updated.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == id && a.HotelId == hotelId.Value);

        if (asset != null)
        {
            _db.Assets.Remove(asset);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Asset deleted.";
        }

        return RedirectToAction("Index");
    }
}
