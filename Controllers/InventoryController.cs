using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public InventoryController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    public async Task<IActionResult> Index(string type = "market", string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Map department context to default inventory type
        if (!string.IsNullOrEmpty(dept))
        {
            type = dept.ToLowerInvariant() switch
            {
                "sales" => "market",
                "engineering" => "maintenance",
                "housekeeping" => "cleaning",
                _ => type
            };
        }

        if (type != "market" && type != "cleaning" && type != "maintenance")
            type = "market";

        var categories = await _db.InventoryCategories
            .Where(c => c.HotelId == hotelId.Value && c.Type == type)
            .Include(c => c.Items).ThenInclude(i => i.Transactions)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var allItems = categories.SelectMany(c => c.Items).ToList();

        // All items across all types for global low stock count
        var globalLowStock = await _db.InventoryItems
            .Where(i => i.HotelId == hotelId.Value && i.Quantity <= i.MinStock)
            .CountAsync();

        var totalItems = allItems.Count;
        var lowStockCount = globalLowStock;
        var lowStockTypeCount = allItems.Count(i => i.Quantity <= i.MinStock);

        var todayUtc = DateTime.UtcNow.Date;
        var itemIds = allItems.Select(i => i.Id).ToList();

        var todayTransactions = await _db.InventoryTransactions
            .Where(t => itemIds.Contains(t.ItemId) && t.CreatedAt >= todayUtc)
            .ToListAsync();

        var addedToday = todayTransactions.Where(t => t.Type == "in").Sum(t => t.Quantity);
        var usedToday = todayTransactions.Where(t => t.Type == "out").Sum(t => t.Quantity);

        // Recent transactions for activity log
        var recentTransactions = await _db.InventoryTransactions
            .Where(t => itemIds.Contains(t.ItemId))
            .Include(t => t.Item)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.CurrentType = type;
        ViewBag.CurrentDept = dept;
        ViewBag.TotalItems = totalItems;
        ViewBag.LowStockCount = lowStockCount;
        ViewBag.LowStockTypeCount = lowStockTypeCount;
        ViewBag.AddedToday = addedToday;
        ViewBag.UsedToday = usedToday;
        ViewBag.RecentTransactions = recentTransactions;

        return View(categories);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(string name, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (type != "market" && type != "cleaning" && type != "maintenance")
            type = "market";

        var category = new InventoryCategory
        {
            HotelId = hotelId.Value,
            Name = name.Trim(),
            Type = type
        };

        _db.InventoryCategories.Add(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category created.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem(int categoryId, string name, int quantity, string unit, int minStock, string? location, decimal? cost, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Item name is required.";
            return RedirectToAction("Index", new { type, dept });
        }

        var category = await _db.InventoryCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.HotelId == hotelId.Value);
        if (category == null)
        {
            TempData["Error"] = "Category not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        var item = new InventoryItem
        {
            CategoryId = categoryId,
            HotelId = hotelId.Value,
            Name = name.Trim(),
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(unit) ? "unit" : unit.Trim(),
            MinStock = minStock,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Cost = cost,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"'{item.Name}' added.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> AddStock(int itemId, int quantity, string? notes, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (quantity <= 0)
        {
            TempData["Error"] = "Quantity must be greater than zero.";
            return RedirectToAction("Index", new { type, dept });
        }

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = itemId,
            Quantity = quantity,
            Type = "in",
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity += quantity;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"+{quantity} {item.Unit}(s) to {item.Name}.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveStock(int itemId, int quantity, string? notes, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (quantity <= 0)
        {
            TempData["Error"] = "Quantity must be greater than zero.";
            return RedirectToAction("Index", new { type, dept });
        }

        // Don't go below 0
        var actualRemove = Math.Min(quantity, item.Quantity);
        if (actualRemove == 0)
        {
            TempData["Error"] = "Stock is already at 0.";
            return RedirectToAction("Index", new { type, dept });
        }

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = itemId,
            Quantity = actualRemove,
            Type = "out",
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity -= actualRemove;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"-{actualRemove} {item.Unit}(s) from {item.Name}.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> AdjustStock(int itemId, int newQuantity, string? notes, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (newQuantity < 0) newQuantity = 0;

        var diff = newQuantity - item.Quantity;
        if (diff == 0)
        {
            TempData["Success"] = $"{item.Name} stock unchanged ({newQuantity}).";
            return RedirectToAction("Index", new { type, dept });
        }

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = itemId,
            Quantity = Math.Abs(diff),
            Type = diff > 0 ? "in" : "out",
            Notes = string.IsNullOrWhiteSpace(notes) ? $"Adjusted to {newQuantity}" : notes.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity = newQuantity;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{item.Name} stock set to {newQuantity}.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> EditItem(int id, string name, string unit, int minStock, string? location, decimal? cost, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == id && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Item name is required.";
            return RedirectToAction("Index", new { type, dept });
        }

        item.Name = name.Trim();
        item.Unit = string.IsNullOrWhiteSpace(unit) ? "unit" : unit.Trim();
        item.MinStock = minStock;
        item.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        item.Cost = cost;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"'{item.Name}' updated.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteItem(int id, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == id && i.HotelId == hotelId.Value);
        if (item == null)
        {
            TempData["Error"] = "Item not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Item deleted.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, string type, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var category = await _db.InventoryCategories
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);
        if (category == null)
        {
            TempData["Error"] = "Category not found.";
            return RedirectToAction("Index", new { type, dept });
        }

        if (category.Items.Any())
        {
            TempData["Error"] = "Cannot delete category with items. Remove all items first.";
            return RedirectToAction("Index", new { type, dept });
        }

        _db.InventoryCategories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category deleted.";
        return RedirectToAction("Index", new { type, dept });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RemoveStockAjax([FromBody] AjaxStockRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Unauthorized();

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.HotelId == hotelId.Value);
        if (item == null) return NotFound(new { error = "Item not found." });

        var qty = request.Quantity > 0 ? request.Quantity : 1;
        var actualRemove = Math.Min(qty, item.Quantity);
        if (actualRemove == 0)
            return Json(new { success = true, newQuantity = item.Quantity, itemName = item.Name, unit = item.Unit, isLow = item.Quantity <= item.MinStock, minStock = item.MinStock });

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = request.ItemId,
            Quantity = actualRemove,
            Type = "out",
            Notes = request.Notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity -= actualRemove;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        return Json(new { success = true, newQuantity = item.Quantity, removed = actualRemove, itemName = item.Name, unit = item.Unit, isLow = item.Quantity <= item.MinStock, minStock = item.MinStock });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddStockAjax([FromBody] AjaxStockRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Unauthorized();

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.HotelId == hotelId.Value);
        if (item == null) return NotFound(new { error = "Item not found." });

        var qty = request.Quantity > 0 ? request.Quantity : 1;

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = request.ItemId,
            Quantity = qty,
            Type = "in",
            Notes = request.Notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity += qty;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        return Json(new { success = true, newQuantity = item.Quantity, added = qty, itemName = item.Name, unit = item.Unit, isLow = item.Quantity <= item.MinStock, minStock = item.MinStock });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AdjustStockAjax([FromBody] AjaxStockRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Unauthorized();

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.HotelId == hotelId.Value);
        if (item == null) return NotFound(new { error = "Item not found." });

        var newQty = request.Quantity < 0 ? 0 : request.Quantity;
        var diff = newQty - item.Quantity;
        if (diff == 0)
            return Json(new { success = true, newQuantity = item.Quantity, itemName = item.Name, unit = item.Unit, isLow = item.Quantity <= item.MinStock, minStock = item.MinStock });

        var createdBy = User.FindFirstValue(ClaimTypes.Name)
                        ?? User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System";

        var transaction = new InventoryTransaction
        {
            ItemId = request.ItemId,
            Quantity = Math.Abs(diff),
            Type = diff > 0 ? "in" : "out",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? $"Adjusted to {newQty}" : request.Notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity = newQty;
        _db.InventoryTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        return Json(new { success = true, newQuantity = item.Quantity, itemName = item.Name, unit = item.Unit, isLow = item.Quantity <= item.MinStock, minStock = item.MinStock });
    }

    public class AjaxStockRequest
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions(int itemId)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Unauthorized();

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HotelId == hotelId.Value);
        if (item == null) return NotFound();

        var transactions = await _db.InventoryTransactions
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new
            {
                t.Id,
                t.Quantity,
                t.Type,
                t.Notes,
                t.CreatedBy,
                CreatedAt = t.CreatedAt.ToString("MMM dd, yyyy h:mm tt")
            })
            .ToListAsync();

        return Json(transactions);
    }
}
