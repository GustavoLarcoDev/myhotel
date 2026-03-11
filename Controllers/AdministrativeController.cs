using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class AdministrativeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;

    public AdministrativeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, HotelContextService hotelContext, NotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var now = DateTime.UtcNow;

        // Expenses this month
        var expenses = await _db.Expenses
            .Where(e => e.HotelId == hotelId.Value && e.Date.Year == now.Year && e.Date.Month == now.Month)
            .ToListAsync();

        var expensesTotal = expenses.Sum(e => e.Amount);
        ViewBag.MonthlyExpenses = expensesTotal;

        // Budget items this month
        var period = now.ToString("yyyy-MM");
        var budgetItems = await _db.BudgetItems
            .Where(b => b.HotelId == hotelId.Value && b.Period == period)
            .ToListAsync();

        var budgetTotal = budgetItems.Sum(b => b.PlannedAmount);
        var budgetVariance = budgetTotal - expensesTotal;
        ViewBag.BudgetTotal = budgetTotal;
        ViewBag.BudgetVariance = budgetVariance;

        // Evaluations count
        var evaluationsCount = await _db.Evaluations
            .CountAsync(e => e.HotelId == hotelId.Value);
        ViewBag.EvaluationsCount = evaluationsCount;

        // Vendors count
        var vendorsCount = await _db.Vendors
            .CountAsync(v => v.HotelId == hotelId.Value);
        ViewBag.VendorsCount = vendorsCount;

        return View();
    }

    public async Task<IActionResult> Staff()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Get all users assigned to this hotel
        var userRoles = await _db.UserHotelRoles
            .Include(ur => ur.User)
            .Where(ur => ur.HotelId == hotelId.Value)
            .ToListAsync();

        var userIds = userRoles.Select(ur => ur.UserId).Distinct().ToList();

        var userDepartments = await _db.UserDepartments
            .Include(ud => ud.Department)
            .Where(ud => userIds.Contains(ud.UserId) && ud.Department.HotelId == hotelId.Value)
            .ToListAsync();

        // Filter to only Administrative department staff
        var adminDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value && d.Name == "Administrative");

        var staffList = userRoles
            .GroupBy(ur => ur.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                var role = g.OrderBy(ur => ur.Role).First().Role;
                var depts = userDepartments.Where(ud => ud.UserId == user.Id).Select(ud => ud.Department).ToList();
                return new
                {
                    User = user,
                    Role = role,
                    Departments = depts
                };
            })
            .Where(s => adminDept != null && s.Departments.Any(d => d.Id == adminDept.Id))
            .OrderBy(s => s.Role)
            .ThenBy(s => s.User.FullName)
            .ToList();

        ViewBag.StaffList = staffList;
        ViewBag.DepartmentName = adminDept?.Name ?? "Administrative";

        return View();
    }
}
