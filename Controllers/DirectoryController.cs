using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class DirectoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public DirectoryController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    public async Task<IActionResult> Index(string tab = "staff", string search = "", int? departmentId = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        ViewBag.CurrentTab = tab;
        ViewBag.CurrentSearch = search ?? "";
        ViewBag.CurrentDepartmentId = departmentId;

        // Staff
        var userRoles = await _db.UserHotelRoles
            .Include(ur => ur.User)
            .Where(ur => ur.HotelId == hotelId.Value)
            .ToListAsync();

        var userIds = userRoles.Select(ur => ur.UserId).Distinct().ToList();

        var userDepartments = await _db.UserDepartments
            .Include(ud => ud.Department)
            .Where(ud => userIds.Contains(ud.UserId) && ud.Department.HotelId == hotelId.Value)
            .ToListAsync();

        var departments = await _db.Departments
            .Where(d => d.HotelId == hotelId.Value)
            .OrderBy(d => d.Name)
            .ToListAsync();

        ViewBag.Departments = departments;

        // Build staff list
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
                    Departments = depts,
                    DepartmentIds = depts.Select(d => d.Id).ToList()
                };
            })
            .ToList();

        // Filter by department
        if (departmentId.HasValue)
        {
            staffList = staffList.Where(s => s.DepartmentIds.Contains(departmentId.Value)).ToList();
        }

        // Filter by search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            staffList = staffList.Where(st =>
                st.User.FullName.ToLower().Contains(s) ||
                (st.User.Email != null && st.User.Email.ToLower().Contains(s)) ||
                (st.User.Phone != null && st.User.Phone.ToLower().Contains(s))
            ).ToList();
        }

        ViewBag.StaffList = staffList.OrderBy(s => s.Role).ThenBy(s => s.User.FullName).ToList();

        // Vendors
        var vendorsQuery = _db.Vendors.Where(v => v.HotelId == hotelId.Value);
        if (!string.IsNullOrWhiteSpace(search) && tab == "vendors")
        {
            var s = search.Trim().ToLower();
            vendorsQuery = vendorsQuery.Where(v =>
                v.Name.ToLower().Contains(s) ||
                (v.Service != null && v.Service.ToLower().Contains(s)) ||
                (v.Email != null && v.Email.ToLower().Contains(s)));
        }
        var vendors = await vendorsQuery.OrderBy(v => v.Name).ToListAsync();
        ViewBag.Vendors = vendors;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateVendor(string name, string? service, string? phone, string? email, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Vendor name is required.";
            return RedirectToAction("Index", new { tab = "vendors" });
        }

        var vendor = new Vendor
        {
            HotelId = hotelId.Value,
            Name = name.Trim(),
            Service = service?.Trim(),
            Phone = phone?.Trim(),
            Email = email?.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor created.";
        return RedirectToAction("Index", new { tab = "vendors" });
    }

    [HttpPost]
    public async Task<IActionResult> EditVendor(int id, string name, string? service, string? phone, string? email, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && v.HotelId == hotelId.Value);
        if (vendor == null)
        {
            TempData["Error"] = "Vendor not found.";
            return RedirectToAction("Index", new { tab = "vendors" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Vendor name is required.";
            return RedirectToAction("Index", new { tab = "vendors" });
        }

        vendor.Name = name.Trim();
        vendor.Service = service?.Trim();
        vendor.Phone = phone?.Trim();
        vendor.Email = email?.Trim();
        vendor.Notes = notes?.Trim();

        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor updated.";
        return RedirectToAction("Index", new { tab = "vendors" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteVendor(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.Id == id && v.HotelId == hotelId.Value);
        if (vendor == null)
        {
            TempData["Error"] = "Vendor not found.";
            return RedirectToAction("Index", new { tab = "vendors" });
        }

        _db.Vendors.Remove(vendor);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vendor deleted.";
        return RedirectToAction("Index", new { tab = "vendors" });
    }
}
