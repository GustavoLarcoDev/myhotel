using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class BudgetController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notifications;

    public BudgetController(ApplicationDbContext db, HotelContextService hotelContext, UserManager<ApplicationUser> userManager, NotificationService notifications)
    {
        _db = db;
        _hotelContext = hotelContext;
        _userManager = userManager;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(string period = "", string view = "expenses")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(period))
            period = DateTime.UtcNow.ToString("yyyy-MM");

        ViewBag.CurrentPeriod = period;
        ViewBag.CurrentView = view;

        // Parse period
        if (!DateTime.TryParse(period + "-01", out var periodDate))
            periodDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        // Expenses for the period
        var expenses = await _db.Expenses
            .Include(e => e.Vendor)
            .Where(e => e.HotelId == hotelId.Value && e.Date.Year == periodDate.Year && e.Date.Month == periodDate.Month)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        ViewBag.Expenses = expenses;
        ViewBag.ExpensesTotal = expenses.Sum(e => e.Amount);
        ViewBag.ExpensesByCategory = expenses
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        // Budget items for the period
        var budgetItems = await _db.BudgetItems
            .Where(b => b.HotelId == hotelId.Value && b.Period == period)
            .OrderBy(b => b.Category)
            .ToListAsync();

        ViewBag.BudgetItems = budgetItems;
        ViewBag.BudgetTotal = budgetItems.Sum(b => b.PlannedAmount);

        // Budget vs Actual
        var categories = new[] { "Maintenance", "Housekeeping", "Front Desk", "Food & Beverage", "Utilities", "Marketing", "Payroll", "Other" };
        var budgetVsActual = categories.Select(cat =>
        {
            var planned = budgetItems.Where(b => b.Category == cat).Sum(b => b.PlannedAmount);
            var actual = expenses.Where(e => e.Category == cat).Sum(e => e.Amount);
            var variance = planned - actual;
            var percentUsed = planned > 0 ? Math.Round((double)(actual / planned) * 100, 1) : 0;
            return new { Category = cat, Planned = planned, Actual = actual, Variance = variance, PercentUsed = percentUsed };
        }).ToList();

        ViewBag.BudgetVsActual = budgetVsActual;

        // Vendors for dropdown
        var vendors = await _db.Vendors
            .Where(v => v.HotelId == hotelId.Value)
            .OrderBy(v => v.Name)
            .ToListAsync();
        ViewBag.Vendors = vendors;

        ViewBag.Categories = categories;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense(string description, decimal amount, string category, int? vendorId, DateTime date, string createdBy, string period)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(description) || amount <= 0)
        {
            TempData["Error"] = "Description and a valid amount are required.";
            return RedirectToAction("Index", new { period, view = "expenses" });
        }

        var user = await _userManager.GetUserAsync(User);

        var expense = new Expense
        {
            HotelId = hotelId.Value,
            Description = description.Trim(),
            Amount = amount,
            Category = category ?? "Other",
            VendorId = vendorId,
            Date = date == default ? DateTime.UtcNow : date,
            CreatedBy = user?.FullName ?? "System",
            CreatedAt = DateTime.UtcNow
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        // Notify GMs about the new expense
        var gmIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value && (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
            .Select(r => r.UserId).Distinct().ToListAsync();
        foreach (var gmId in gmIds)
        {
            await _notifications.CreateAsync(hotelId.Value, gmId, $"New Expense: ${amount}",
                $"{description.Trim()} - {category ?? "Other"}", "/Budget", "info");
        }

        TempData["Success"] = "Expense created.";
        return RedirectToAction("Index", new { period, view = "expenses" });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBudget(string category, decimal plannedAmount, string period)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(category) || plannedAmount <= 0)
        {
            TempData["Error"] = "Category and a valid planned amount are required.";
            return RedirectToAction("Index", new { period, view = "budget" });
        }

        // Check if budget item already exists for this category and period
        var existing = await _db.BudgetItems.FirstOrDefaultAsync(b =>
            b.HotelId == hotelId.Value && b.Category == category && b.Period == period);

        if (existing != null)
        {
            existing.PlannedAmount = plannedAmount;
        }
        else
        {
            var budgetItem = new BudgetItem
            {
                HotelId = hotelId.Value,
                Category = category,
                PlannedAmount = plannedAmount,
                Period = period,
                CreatedAt = DateTime.UtcNow
            };
            _db.BudgetItems.Add(budgetItem);
        }

        await _db.SaveChangesAsync();

        // Notify GMs about the budget update
        var gmIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value && (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
            .Select(r => r.UserId).Distinct().ToListAsync();
        foreach (var gmId in gmIds)
        {
            await _notifications.CreateAsync(hotelId.Value, gmId, $"Budget Updated: {category}",
                $"Planned amount set to ${plannedAmount} for {period}", "/Budget", "info");
        }

        TempData["Success"] = "Budget item saved.";
        return RedirectToAction("Index", new { period, view = "budget" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteExpense(int id, string period)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.HotelId == hotelId.Value);
        if (expense == null)
        {
            TempData["Error"] = "Expense not found.";
            return RedirectToAction("Index", new { period, view = "expenses" });
        }

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Expense deleted.";
        return RedirectToAction("Index", new { period, view = "expenses" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBudget(int id, string period)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var budgetItem = await _db.BudgetItems.FirstOrDefaultAsync(b => b.Id == id && b.HotelId == hotelId.Value);
        if (budgetItem == null)
        {
            TempData["Error"] = "Budget item not found.";
            return RedirectToAction("Index", new { period, view = "budget" });
        }

        _db.BudgetItems.Remove(budgetItem);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Budget item deleted.";
        return RedirectToAction("Index", new { period, view = "budget" });
    }
}
