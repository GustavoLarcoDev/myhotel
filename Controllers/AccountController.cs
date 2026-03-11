using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Models.ViewModels;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        HotelContextService hotelContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _hotelContext = hotelContext;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Try to find user by email or phone
        var user = await _userManager.FindByEmailAsync(model.EmailOrPhone);
        if (user == null)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == model.EmailOrPhone);
        }

        if (user == null)
        {
            ModelState.AddModelError("", "Invalid credentials.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError("", "Account is deactivated.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            // Auto-select first hotel
            var firstHotelRole = await _db.UserHotelRoles
                .Include(r => r.Hotel)
                .Where(r => r.UserId == user.Id)
                .FirstOrDefaultAsync();

            if (firstHotelRole != null)
            {
                await _hotelContext.SetCurrentHotel(firstHotelRole.HotelId);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Account temporarily locked. Try again in 15 minutes.");
            return View(model);
        }

        ModelState.AddModelError("", "Invalid credentials.");
        return View(model);
    }

    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }
}
