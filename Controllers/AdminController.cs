using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Models.ViewModels;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ImpersonationService _impersonation;
    private readonly HotelContextService _hotelContext;

    public AdminController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ImpersonationService impersonation,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _impersonation = impersonation;
        _hotelContext = hotelContext;
    }

    private async Task<bool> IsAdmin()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return false;
        return await _db.UserHotelRoles.AnyAsync(r => r.UserId == user.Id && r.Role == AppRole.Admin);
    }

    // GET: /Admin
    public async Task<IActionResult> Index()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        // Get all GMs (users who have GeneralManager role in any hotel)
        var gms = await _db.UserHotelRoles
            .Where(r => r.Role == AppRole.GeneralManager)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        var gmUsers = await _db.Users
            .Where(u => gms.Contains(u.Id))
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        ViewBag.TotalGMs = gmUsers.Count;
        ViewBag.ActiveGMs = gmUsers.Count(u => u.IsActive);
        ViewBag.TotalHotels = await _db.Hotels.CountAsync();

        return View(gmUsers);
    }

    // GET: /Admin/CreateGM
    [HttpGet]
    public async Task<IActionResult> CreateGM()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");
        return View(new AdminCreateGMViewModel());
    }

    // POST: /Admin/CreateGM
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGM(AdminCreateGMViewModel model)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        if (!ModelState.IsValid) return View(model);

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email already exists.");
            return View(model);
        }

        // Create the hotel first
        var hotel = new Hotel
        {
            Name = model.HotelName,
            Address = model.HotelAddress,
            City = model.HotelCity,
            State = model.HotelState,
            Phone = model.HotelPhone
        };

        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        // Create default departments
        var defaultDepartments = new[] { "Front Desk", "Housekeeping", "Maintenance", "Security", "Food & Beverage" };
        foreach (var dept in defaultDepartments)
        {
            _db.Departments.Add(new Department { Name = dept, HotelId = hotel.Id });
        }
        await _db.SaveChangesAsync();

        // Create the GM user
        var gmUser = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Phone = model.Phone,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(gmUser, model.Password);
        if (!result.Succeeded)
        {
            // Clean up the hotel we just created
            _db.Hotels.Remove(hotel);
            await _db.SaveChangesAsync();

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // Assign GM role to hotel
        _db.UserHotelRoles.Add(new UserHotelRole
        {
            UserId = gmUser.Id,
            HotelId = hotel.Id,
            Role = AppRole.GeneralManager
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"GM \"{gmUser.FullName}\" created with hotel \"{hotel.Name}\".";
        return RedirectToAction("Index");
    }

    // GET: /Admin/EditGM/{id}
    [HttpGet]
    public async Task<IActionResult> EditGM(string id)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var user = await _db.Users
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        // Verify this user is actually a GM
        var isGM = user.HotelRoles.Any(r => r.Role == AppRole.GeneralManager);
        if (!isGM) return NotFound();

        return View(user);
    }

    // POST: /Admin/EditGM/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGM(string id, string firstName, string lastName, string? phone, bool isActive)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var user = await _db.Users
            .Include(u => u.HotelRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        // Verify this user is actually a GM
        var isGM = user.HotelRoles.Any(r => r.Role == AppRole.GeneralManager);
        if (!isGM) return NotFound();

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Phone = phone;
        user.IsActive = isActive;

        await _db.SaveChangesAsync();

        TempData["Success"] = $"GM \"{user.FullName}\" updated successfully.";
        return RedirectToAction("Index");
    }

    // GET: /Admin/Impersonate?userId=abc123
    public async Task<IActionResult> Impersonate(string userId)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return RedirectToAction("Login", "Account");

        var targetUser = await _userManager.FindByIdAsync(userId);
        if (targetUser == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index");
        }

        _impersonation.StartImpersonation(currentUser.Id, targetUser.Id);

        // Set hotel context to target user's first hotel
        var targetHotelRole = await _db.UserHotelRoles
            .Where(r => r.UserId == targetUser.Id)
            .FirstOrDefaultAsync();

        if (targetHotelRole != null)
        {
            HttpContext.Session.SetInt32("CurrentHotelId", targetHotelRole.HotelId);
        }

        TempData["Success"] = $"Now impersonating {targetUser.FullName}.";
        return RedirectToAction("Index", "Dashboard");
    }

    // GET: /Admin/StopImpersonation
    public async Task<IActionResult> StopImpersonation()
    {
        if (!_impersonation.IsImpersonating) return RedirectToAction("Index");

        var originalUserId = _impersonation.OriginalUserId;
        _impersonation.StopImpersonation();

        // Restore admin's hotel context
        if (originalUserId != null)
        {
            var adminHotelRole = await _db.UserHotelRoles
                .Where(r => r.UserId == originalUserId)
                .FirstOrDefaultAsync();

            if (adminHotelRole != null)
            {
                HttpContext.Session.SetInt32("CurrentHotelId", adminHotelRole.HotelId);
            }
        }

        TempData["Success"] = "Impersonation stopped.";
        return RedirectToAction("Index");
    }
}
