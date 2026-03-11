using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class EvaluationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;

    public EvaluationsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
    }

    // GET: /Evaluations?tab=evaluations|hkratings
    public async Task<IActionResult> Index(string tab = "evaluations")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        ViewBag.Tab = tab;

        // Get all hotel employees
        var hotelUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        var employees = await _db.Users
            .Where(u => hotelUserIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        // Get user departments for display
        var userDepartments = await _db.UserDepartments
            .Where(ud => hotelUserIds.Contains(ud.UserId))
            .Include(ud => ud.Department)
            .ToListAsync();

        ViewBag.Employees = employees;
        ViewBag.UserDepartments = userDepartments;

        if (tab == "evaluations")
        {
            var evaluations = await _db.Evaluations
                .Where(e => e.HotelId == hotelId.Value)
                .Include(e => e.Employee)
                .Include(e => e.Evaluator)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            ViewBag.Evaluations = evaluations;

            // Group by employee for summary cards
            var evalsByEmployee = evaluations
                .GroupBy(e => e.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Employee = g.First().Employee,
                    AvgRating = g.Average(e => e.Rating),
                    Count = g.Count(),
                    Evaluations = g.OrderByDescending(e => e.CreatedAt).ToList()
                })
                .OrderBy(x => x.Employee.FirstName)
                .ThenBy(x => x.Employee.LastName)
                .ToList();

            ViewBag.EvalsByEmployee = evalsByEmployee;
        }
        else
        {
            // HK Ratings tab
            var hkRatings = await _db.HousekeepingRatings
                .Where(r => r.HotelId == hotelId.Value)
                .Include(r => r.Housekeeper)
                .Include(r => r.RatedBy)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.HkRatings = hkRatings;

            // Group by housekeeper for summary
            var ratingsByHousekeeper = hkRatings
                .GroupBy(r => r.HousekeeperId)
                .Select(g => new
                {
                    HousekeeperId = g.Key,
                    Housekeeper = g.First().Housekeeper,
                    AvgStars = g.Average(r => r.Stars),
                    Count = g.Count(),
                    Ratings = g.OrderByDescending(r => r.Date).ToList()
                })
                .OrderBy(x => x.Housekeeper.FirstName)
                .ThenBy(x => x.Housekeeper.LastName)
                .ToList();

            ViewBag.RatingsByHousekeeper = ratingsByHousekeeper;

            // Get housekeeping department employees for the dropdown
            var hkDepartments = await _db.Departments
                .Where(d => d.HotelId == hotelId.Value &&
                    (d.Name.ToLower().Contains("housekeeping") || d.Name.ToLower().Contains("hk")))
                .Select(d => d.Id)
                .ToListAsync();

            List<ApplicationUser> housekeepers;
            if (hkDepartments.Any())
            {
                var hkUserIds = await _db.UserDepartments
                    .Where(ud => hkDepartments.Contains(ud.DepartmentId))
                    .Select(ud => ud.UserId)
                    .Distinct()
                    .ToListAsync();

                housekeepers = await _db.Users
                    .Where(u => hkUserIds.Contains(u.Id) && u.IsActive)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();
            }
            else
            {
                // If no HK department found, show all employees
                housekeepers = employees;
            }

            ViewBag.Housekeepers = housekeepers;
        }

        return View();
    }

    // POST: /Evaluations/CreateEvaluation
    [HttpPost]
    public async Task<IActionResult> CreateEvaluation(string employeeId, int rating, string? comments, string? period)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(employeeId))
        {
            TempData["Error"] = "Please select an employee.";
            return RedirectToAction("Index", new { tab = "evaluations" });
        }

        if (rating < 1 || rating > 5)
        {
            TempData["Error"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Index", new { tab = "evaluations" });
        }

        var evaluation = new Evaluation
        {
            HotelId = hotelId.Value,
            EmployeeId = employeeId,
            EvaluatorId = user.Id,
            Rating = rating,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            Period = string.IsNullOrWhiteSpace(period) ? null : period.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Evaluations.Add(evaluation);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Evaluation submitted.";
        return RedirectToAction("Index", new { tab = "evaluations" });
    }

    // POST: /Evaluations/CreateHkRating
    [HttpPost]
    public async Task<IActionResult> CreateHkRating(string housekeeperId, int stars, string? room, string? comments, DateTime date)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(housekeeperId))
        {
            TempData["Error"] = "Please select a housekeeper.";
            return RedirectToAction("Index", new { tab = "hkratings" });
        }

        if (stars < 1 || stars > 5)
        {
            TempData["Error"] = "Stars must be between 1 and 5.";
            return RedirectToAction("Index", new { tab = "hkratings" });
        }

        var rating = new HousekeepingRating
        {
            HotelId = hotelId.Value,
            HousekeeperId = housekeeperId,
            RatedById = user.Id,
            Stars = stars,
            Room = string.IsNullOrWhiteSpace(room) ? null : room.Trim(),
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            Date = date.Date,
            CreatedAt = DateTime.UtcNow
        };

        _db.HousekeepingRatings.Add(rating);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Housekeeping rating submitted.";
        return RedirectToAction("Index", new { tab = "hkratings" });
    }

    // POST: /Evaluations/Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var evaluation = await _db.Evaluations
            .FirstOrDefaultAsync(e => e.Id == id && e.HotelId == hotelId.Value);

        if (evaluation == null)
        {
            TempData["Error"] = "Evaluation not found.";
            return RedirectToAction("Index", new { tab = "evaluations" });
        }

        _db.Evaluations.Remove(evaluation);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Evaluation deleted.";
        return RedirectToAction("Index", new { tab = "evaluations" });
    }

    // POST: /Evaluations/DeleteHkRating
    [HttpPost]
    public async Task<IActionResult> DeleteHkRating(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var rating = await _db.HousekeepingRatings
            .FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);

        if (rating == null)
        {
            TempData["Error"] = "Rating not found.";
            return RedirectToAction("Index", new { tab = "hkratings" });
        }

        _db.HousekeepingRatings.Remove(rating);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Housekeeping rating deleted.";
        return RedirectToAction("Index", new { tab = "hkratings" });
    }
}
