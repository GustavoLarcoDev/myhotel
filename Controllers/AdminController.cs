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
    private readonly PermissionService _permissions;
    private readonly ImpersonationService _impersonation;

    public AdminController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        PermissionService permissions,
        ImpersonationService impersonation)
    {
        _db = db;
        _userManager = userManager;
        _permissions = permissions;
        _impersonation = impersonation;
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

        ViewBag.HotelCount = await _db.Hotels.CountAsync();
        ViewBag.UserCount = await _userManager.Users.CountAsync();
        ViewBag.ActiveUsers = await _userManager.Users.CountAsync(u => u.IsActive);
        ViewBag.DepartmentCount = await _db.Departments.CountAsync();
        ViewBag.RoleAssignments = await _db.UserHotelRoles.CountAsync();

        return View();
    }

    // GET: /Admin/Hotels
    public async Task<IActionResult> Hotels()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotels = await _db.Hotels
            .Include(h => h.UserRoles)
                .ThenInclude(r => r.User)
            .Include(h => h.Departments)
            .Include(h => h.Rooms)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return View(hotels);
    }

    // GET: /Admin/CreateHotel
    [HttpGet]
    public async Task<IActionResult> CreateHotel()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");
        return View(new AdminCreateHotelViewModel());
    }

    // POST: /Admin/CreateHotel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHotel(AdminCreateHotelViewModel model)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        if (!ModelState.IsValid) return View(model);

        var hotel = new Hotel
        {
            Name = model.Name,
            Address = model.Address,
            City = model.City,
            State = model.State,
            Phone = model.Phone
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

        // Create GM if provided
        if (!string.IsNullOrWhiteSpace(model.GmEmail) && !string.IsNullOrWhiteSpace(model.GmPassword))
        {
            var gmUser = new ApplicationUser
            {
                UserName = model.GmEmail,
                Email = model.GmEmail,
                FirstName = model.GmFirstName ?? "",
                LastName = model.GmLastName ?? "",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(gmUser, model.GmPassword);
            if (result.Succeeded)
            {
                _db.UserHotelRoles.Add(new UserHotelRole
                {
                    UserId = gmUser.Id,
                    HotelId = hotel.Id,
                    Role = AppRole.GeneralManager
                });
                await _db.SaveChangesAsync();
            }
            else
            {
                TempData["Error"] = "Hotel created but GM account failed: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction("Hotels");
            }
        }

        TempData["Success"] = $"Hotel \"{hotel.Name}\" created successfully.";
        return RedirectToAction("Hotels");
    }

    // GET: /Admin/EditHotel/5
    [HttpGet]
    public async Task<IActionResult> EditHotel(int id)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null) return NotFound();

        return View(hotel);
    }

    // POST: /Admin/EditHotel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHotel(int id, Hotel model)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null) return NotFound();

        hotel.Name = model.Name;
        hotel.Address = model.Address;
        hotel.City = model.City;
        hotel.State = model.State;
        hotel.Phone = model.Phone;
        hotel.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Hotel updated successfully.";
        return RedirectToAction("Hotels");
    }

    // POST: /Admin/DeleteHotel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHotel(int id)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null) return NotFound();

        _db.Hotels.Remove(hotel);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Hotel deleted successfully.";
        return RedirectToAction("Hotels");
    }

    // GET: /Admin/Users
    public async Task<IActionResult> Users(int? hotelId)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var usersQuery = _db.Users
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .AsQueryable();

        if (hotelId.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.HotelRoles.Any(r => r.HotelId == hotelId.Value));
        }

        var users = await usersQuery.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

        ViewBag.Hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
        ViewBag.SelectedHotelId = hotelId;

        return View(users);
    }

    // GET: /Admin/CreateUser
    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        ViewBag.Hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
        ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();

        return View(new AdminCreateUserViewModel());
    }

    // POST: /Admin/CreateUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        if (!ModelState.IsValid)
        {
            ViewBag.Hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
            ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Phone = model.Phone,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.Hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
            ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(model);
        }

        // Assign role to hotel
        if (model.HotelId > 0)
        {
            _db.UserHotelRoles.Add(new UserHotelRole
            {
                UserId = user.Id,
                HotelId = model.HotelId,
                Role = model.Role
            });

            // Assign department if selected
            if (model.DepartmentId.HasValue && model.DepartmentId.Value > 0)
            {
                _db.UserDepartments.Add(new UserDepartment
                {
                    UserId = user.Id,
                    DepartmentId = model.DepartmentId.Value,
                    IsManager = model.Role == AppRole.DepartmentManager
                });
            }

            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"User \"{user.FullName}\" created successfully.";
        return RedirectToAction("Users");
    }

    // GET: /Admin/EditUser/abc123
    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var user = await _db.Users
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .Include(u => u.Departments)
                .ThenInclude(d => d.Department)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        ViewBag.Hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
        ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();

        return View(user);
    }

    // POST: /Admin/EditUser/abc123
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, string firstName, string lastName, string? phone,
        bool isActive, int hotelId, AppRole role, int? departmentId)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var user = await _db.Users
            .Include(u => u.HotelRoles)
            .Include(u => u.Departments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Phone = phone;
        user.IsActive = isActive;

        // Update hotel role
        if (hotelId > 0)
        {
            var existingRole = user.HotelRoles.FirstOrDefault(r => r.HotelId == hotelId);
            if (existingRole != null)
            {
                existingRole.Role = role;
            }
            else
            {
                _db.UserHotelRoles.Add(new UserHotelRole
                {
                    UserId = user.Id,
                    HotelId = hotelId,
                    Role = role
                });
            }

            // Update department
            var existingDept = user.Departments.FirstOrDefault();
            if (departmentId.HasValue && departmentId.Value > 0)
            {
                if (existingDept != null)
                {
                    existingDept.DepartmentId = departmentId.Value;
                    existingDept.IsManager = role == AppRole.DepartmentManager;
                }
                else
                {
                    _db.UserDepartments.Add(new UserDepartment
                    {
                        UserId = user.Id,
                        DepartmentId = departmentId.Value,
                        IsManager = role == AppRole.DepartmentManager
                    });
                }
            }
            else if (existingDept != null)
            {
                _db.UserDepartments.Remove(existingDept);
            }
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"User \"{user.FullName}\" updated successfully.";
        return RedirectToAction("Users");
    }

    // GET: /Admin/Roles
    public async Task<IActionResult> Roles()
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var permissions = await _db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync();
        var rolePermissions = await _db.RolePermissions.ToListAsync();

        ViewBag.RolePermissions = rolePermissions;
        ViewBag.Roles = Enum.GetValues<AppRole>();

        return View(permissions);
    }

    // GET: /Admin/Departments
    public async Task<IActionResult> Departments(int? hotelId)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotels = await _db.Hotels.OrderBy(h => h.Name).ToListAsync();
        ViewBag.Hotels = hotels;
        ViewBag.SelectedHotelId = hotelId ?? hotels.FirstOrDefault()?.Id;

        var departments = await _db.Departments
            .Include(d => d.Hotel)
            .Include(d => d.UserDepartments)
                .ThenInclude(ud => ud.User)
            .Where(d => !hotelId.HasValue || d.HotelId == hotelId.Value)
            .OrderBy(d => d.Hotel.Name)
            .ThenBy(d => d.Name)
            .ToListAsync();

        return View(departments);
    }

    // POST: /Admin/CreateDepartment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartment(string name, int hotelId)
    {
        if (!await IsAdmin()) return RedirectToAction("AccessDenied", "Account");

        if (string.IsNullOrWhiteSpace(name) || hotelId <= 0)
        {
            TempData["Error"] = "Department name and hotel are required.";
            return RedirectToAction("Departments", new { hotelId });
        }

        _db.Departments.Add(new Department
        {
            Name = name.Trim(),
            HotelId = hotelId
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Department \"{name.Trim()}\" created.";
        return RedirectToAction("Departments", new { hotelId });
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
            return RedirectToAction("Users");
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
