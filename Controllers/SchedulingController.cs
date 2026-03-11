using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class SchedulingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;

    public SchedulingController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
    }

    // GET: /Scheduling?departmentId=0&weekStart=2026-03-02
    public async Task<IActionResult> Index(int departmentId = 0, string? weekStart = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        // Parse week start or default to current week's Monday
        DateTime weekStartDate;
        if (!string.IsNullOrEmpty(weekStart) && DateTime.TryParse(weekStart, out var parsed))
        {
            // Snap to Monday
            weekStartDate = parsed.AddDays(-(int)parsed.DayOfWeek + (int)DayOfWeek.Monday);
            if (parsed.DayOfWeek == DayOfWeek.Sunday)
                weekStartDate = parsed.AddDays(-6);
        }
        else
        {
            var today = DateTime.Today;
            var diff = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            weekStartDate = today.AddDays(-diff);
        }

        var weekEndDate = weekStartDate.AddDays(6);

        // Get departments for this hotel
        var departments = await _db.Departments
            .Where(d => d.HotelId == hotelId.Value)
            .OrderBy(d => d.Name)
            .ToListAsync();

        ViewBag.Departments = departments;
        ViewBag.SelectedDepartmentId = departmentId;
        ViewBag.WeekStart = weekStartDate;
        ViewBag.WeekEnd = weekEndDate;
        ViewBag.PrevWeek = weekStartDate.AddDays(-7).ToString("yyyy-MM-dd");
        ViewBag.NextWeek = weekStartDate.AddDays(7).ToString("yyyy-MM-dd");

        // Build week days array
        var weekDays = Enumerable.Range(0, 7)
            .Select(i => weekStartDate.AddDays(i))
            .ToList();
        ViewBag.WeekDays = weekDays;

        if (departmentId == 0)
        {
            ViewBag.Employees = new List<ApplicationUser>();
            ViewBag.Schedules = new List<Schedule>();
            return View();
        }

        // Get employees in the selected department
        var employeeIds = await _db.UserDepartments
            .Where(ud => ud.DepartmentId == departmentId)
            .Select(ud => ud.UserId)
            .ToListAsync();

        var employees = await _db.Users
            .Where(u => employeeIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        // Get schedules for this week and department
        var schedules = await _db.Schedules
            .Where(s => s.HotelId == hotelId.Value
                && s.DepartmentId == departmentId
                && s.Date >= weekStartDate
                && s.Date <= weekEndDate)
            .Include(s => s.Employee)
            .ToListAsync();

        ViewBag.Employees = employees;
        ViewBag.Schedules = schedules;

        return View();
    }

    // POST: /Scheduling/Create
    [HttpPost]
    public async Task<IActionResult> Create(int departmentId, string employeeId, DateTime date, TimeSpan startTime, TimeSpan endTime, string? notes, string? weekStart)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(employeeId) || departmentId == 0)
        {
            TempData["Error"] = "Employee and department are required.";
            return RedirectToAction("Index", new { departmentId, weekStart });
        }

        // Allow overnight shifts (e.g., 11 PM to 7 AM) where endTime < startTime

        var schedule = new Schedule
        {
            HotelId = hotelId.Value,
            DepartmentId = departmentId,
            EmployeeId = employeeId,
            Date = date.Date,
            StartTime = startTime,
            EndTime = endTime,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedBy = user.FullName,
            CreatedAt = DateTime.UtcNow
        };

        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule entry added.";
        return RedirectToAction("Index", new { departmentId, weekStart });
    }

    // POST: /Scheduling/Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id, int departmentId = 0, string? weekStart = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.Id == id && s.HotelId == hotelId.Value);

        if (schedule == null)
        {
            TempData["Error"] = "Schedule entry not found.";
            return RedirectToAction("Index", new { departmentId, weekStart });
        }

        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Schedule entry removed.";
        return RedirectToAction("Index", new { departmentId, weekStart });
    }
}
