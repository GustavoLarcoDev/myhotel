using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class ManagementController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;

    public ManagementController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
    }

    private async Task<bool> IsGMOrAdmin()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return false;
        return await _db.UserHotelRoles.AnyAsync(r =>
            r.UserId == user.Id &&
            (r.Role == AppRole.GeneralManager || r.Role == AppRole.Admin));
    }

    private async Task<List<Hotel>> GetMyHotels()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return new List<Hotel>();

        return await _db.UserHotelRoles
            .Where(r => r.UserId == user.Id && r.Role == AppRole.GeneralManager)
            .Include(r => r.Hotel)
                .ThenInclude(h => h.UserRoles)
            .Include(r => r.Hotel)
                .ThenInclude(h => h.Rooms)
            .Include(r => r.Hotel)
                .ThenInclude(h => h.Departments)
            .Select(r => r.Hotel)
            .OrderBy(h => h.Name)
            .ToListAsync();
    }

    // GET: /Management
    public async Task<IActionResult> Index()
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotels = await GetMyHotels();

        ViewBag.Hotels = hotels;
        ViewBag.HotelCount = hotels.Count;
        ViewBag.TotalUsers = hotels.Sum(h => h.UserRoles.Count);
        ViewBag.TotalRooms = hotels.Sum(h => h.Rooms.Count);
        ViewBag.TotalDepartments = hotels.Sum(h => h.Departments.Count);

        return View();
    }

    // GET: /Management/MyHotels
    public async Task<IActionResult> MyHotels()
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var hotels = await GetMyHotels();
        return View(hotels);
    }

    // GET: /Management/CreateHotel
    [HttpGet]
    public async Task<IActionResult> CreateHotel()
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");
        return View();
    }

    // POST: /Management/CreateHotel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHotel(string name, string? address, string? city, string? state, string? phone)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Hotel name is required.";
            return View();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var hotel = new Hotel
        {
            Name = name.Trim(),
            Address = address?.Trim(),
            City = city?.Trim(),
            State = state?.Trim(),
            Phone = phone?.Trim()
        };

        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        // Create default departments
        var defaultDepartments = new[] { "Front Desk", "Housekeeping", "Maintenance", "Security", "Food & Beverage" };
        foreach (var dept in defaultDepartments)
        {
            _db.Departments.Add(new Department { Name = dept, HotelId = hotel.Id });
        }

        // Auto-assign current user as GM of the new hotel
        _db.UserHotelRoles.Add(new UserHotelRole
        {
            UserId = user.Id,
            HotelId = hotel.Id,
            Role = AppRole.GeneralManager
        });

        await _db.SaveChangesAsync();

        // Set session to the new hotel
        await _hotelContext.SetCurrentHotel(hotel.Id);

        TempData["Success"] = $"Hotel \"{hotel.Name}\" created successfully.";
        return RedirectToAction("MyHotels");
    }

    // GET: /Management/EditHotel/5
    [HttpGet]
    public async Task<IActionResult> EditHotel(int id)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var hotel = myHotels.FirstOrDefault(h => h.Id == id);
        if (hotel == null)
        {
            TempData["Error"] = "Hotel not found or you don't have access.";
            return RedirectToAction("MyHotels");
        }

        return View(hotel);
    }

    // POST: /Management/EditHotel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHotel(int id, string name, string? address, string? city, string? state, string? phone, bool isActive)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        if (!myHotels.Any(h => h.Id == id))
        {
            TempData["Error"] = "Hotel not found or you don't have access.";
            return RedirectToAction("MyHotels");
        }

        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null) return NotFound();

        hotel.Name = name;
        hotel.Address = address;
        hotel.City = city;
        hotel.State = state;
        hotel.Phone = phone;
        hotel.IsActive = isActive;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Hotel updated successfully.";
        return RedirectToAction("MyHotels");
    }

    // GET: /Management/Users?hotelId=5
    public async Task<IActionResult> Users(int? hotelId)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        if (myHotels.Count == 0)
        {
            TempData["Error"] = "You are not a GM of any hotel.";
            return RedirectToAction("Index");
        }

        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        // If hotelId provided, verify it belongs to this GM
        var selectedHotelId = hotelId;
        if (selectedHotelId.HasValue && !myHotelIds.Contains(selectedHotelId.Value))
        {
            selectedHotelId = myHotelIds.First();
        }
        else if (!selectedHotelId.HasValue)
        {
            selectedHotelId = myHotelIds.First();
        }

        var users = await _db.Users
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .Include(u => u.Departments)
                .ThenInclude(d => d.Department)
            .Where(u => u.HotelRoles.Any(r => r.HotelId == selectedHotelId.Value))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        ViewBag.Hotels = myHotels;
        ViewBag.SelectedHotelId = selectedHotelId;

        return View(users);
    }

    // GET: /Management/CreateUser
    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        ViewBag.Hotels = myHotels;
        ViewBag.Departments = await _db.Departments
            .Where(d => myHotels.Select(h => h.Id).Contains(d.HotelId))
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View();
    }

    // POST: /Management/CreateUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string email, string password, string firstName, string lastName,
        string? phone, int hotelId, AppRole role, int? departmentId)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        // Verify hotelId belongs to this GM
        if (!myHotelIds.Contains(hotelId))
        {
            TempData["Error"] = "You don't have access to this hotel.";
            return RedirectToAction("Users");
        }

        // GMs can only assign AssistantGM, DepartmentManager, or Employee roles
        if (role == AppRole.Admin || role == AppRole.GeneralManager)
        {
            TempData["Error"] = "You cannot assign Admin or General Manager roles.";
            ViewBag.Hotels = myHotels;
            ViewBag.Departments = await _db.Departments
                .Where(d => myHotelIds.Contains(d.HotelId))
                .OrderBy(d => d.Name)
                .ToListAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            TempData["Error"] = "Email, password, first name, and last name are required.";
            ViewBag.Hotels = myHotels;
            ViewBag.Departments = await _db.Departments
                .Where(d => myHotelIds.Contains(d.HotelId))
                .OrderBy(d => d.Name)
                .ToListAsync();
            return View();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            TempData["Error"] = "Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description));
            ViewBag.Hotels = myHotels;
            ViewBag.Departments = await _db.Departments
                .Where(d => myHotelIds.Contains(d.HotelId))
                .OrderBy(d => d.Name)
                .ToListAsync();
            return View();
        }

        // Assign role to hotel
        _db.UserHotelRoles.Add(new UserHotelRole
        {
            UserId = user.Id,
            HotelId = hotelId,
            Role = role
        });

        // Assign department if selected
        if (departmentId.HasValue && departmentId.Value > 0)
        {
            _db.UserDepartments.Add(new UserDepartment
            {
                UserId = user.Id,
                DepartmentId = departmentId.Value,
                IsManager = role == AppRole.DepartmentManager
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"User \"{user.FullName}\" created successfully.";
        return RedirectToAction("Users", new { hotelId });
    }

    // GET: /Management/EditUser/abc123
    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        var user = await _db.Users
            .Include(u => u.HotelRoles)
                .ThenInclude(r => r.Hotel)
            .Include(u => u.Departments)
                .ThenInclude(d => d.Department)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        // Verify user belongs to one of GM's hotels
        if (!user.HotelRoles.Any(r => myHotelIds.Contains(r.HotelId)))
        {
            TempData["Error"] = "You don't have access to this user.";
            return RedirectToAction("Users");
        }

        ViewBag.Hotels = myHotels;
        ViewBag.Departments = await _db.Departments
            .Where(d => myHotelIds.Contains(d.HotelId))
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View(user);
    }

    // POST: /Management/EditUser/abc123
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, string firstName, string lastName, string? phone,
        bool isActive, int hotelId, AppRole role, int? departmentId)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        var user = await _db.Users
            .Include(u => u.HotelRoles)
            .Include(u => u.Departments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        // Verify user belongs to one of GM's hotels
        if (!user.HotelRoles.Any(r => myHotelIds.Contains(r.HotelId)))
        {
            TempData["Error"] = "You don't have access to this user.";
            return RedirectToAction("Users");
        }

        // GMs cannot assign Admin or GM roles
        if (role == AppRole.Admin || role == AppRole.GeneralManager)
        {
            TempData["Error"] = "You cannot assign Admin or General Manager roles.";
            return RedirectToAction("EditUser", new { id });
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Phone = phone;
        user.IsActive = isActive;

        // Update hotel role
        if (hotelId > 0 && myHotelIds.Contains(hotelId))
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
        return RedirectToAction("Users", new { hotelId });
    }

    // GET: /Management/Departments?hotelId=5
    public async Task<IActionResult> Departments(int? hotelId)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        var selectedHotelId = hotelId;
        if (selectedHotelId.HasValue && !myHotelIds.Contains(selectedHotelId.Value))
        {
            selectedHotelId = myHotelIds.FirstOrDefault();
        }
        else if (!selectedHotelId.HasValue && myHotelIds.Any())
        {
            selectedHotelId = myHotelIds.First();
        }

        ViewBag.Hotels = myHotels;
        ViewBag.SelectedHotelId = selectedHotelId;

        var departments = await _db.Departments
            .Include(d => d.Hotel)
            .Include(d => d.UserDepartments)
                .ThenInclude(ud => ud.User)
            .Where(d => selectedHotelId.HasValue && d.HotelId == selectedHotelId.Value)
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View(departments);
    }

    // POST: /Management/CreateDepartment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDepartment(string name, int hotelId)
    {
        if (!await IsGMOrAdmin()) return RedirectToAction("AccessDenied", "Account");

        var myHotels = await GetMyHotels();
        var myHotelIds = myHotels.Select(h => h.Id).ToList();

        // Verify hotelId belongs to this GM
        if (!myHotelIds.Contains(hotelId))
        {
            TempData["Error"] = "You don't have access to this hotel.";
            return RedirectToAction("Departments");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Department name is required.";
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
}
