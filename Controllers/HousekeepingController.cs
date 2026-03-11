using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class HousekeepingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;

    public HousekeepingController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext,
        NotificationService notifications)
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

        var today = DateTime.UtcNow.Date;

        var allRequests = await _db.CleaningRequests
            .Where(r => r.HotelId == hotelId.Value)
            .ToListAsync();

        var totalRooms = await _db.Rooms.CountAsync(r => r.HotelId == hotelId.Value);
        var dirtyRooms = await _db.Rooms.CountAsync(r => r.HotelId == hotelId.Value &&
            (r.Status == "vacant-dirty" || r.Status == "occupied"));

        ViewBag.TotalRooms = totalRooms;
        ViewBag.RoomsNeedingCleaning = dirtyRooms;
        ViewBag.PendingRequests = allRequests.Count(r => r.Status == "pending" || r.Status == "in_progress");
        ViewBag.CompletedToday = allRequests.Count(r => r.Status == "completed" && r.CompletedAt?.Date == today);
        ViewBag.TotalRequests = allRequests.Count;

        return View();
    }

    public async Task<IActionResult> CleaningRequests(string status = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var query = _db.CleaningRequests
            .Where(r => r.HotelId == hotelId.Value)
            .Include(r => r.Room);

        var allRequests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var filtered = allRequests.AsEnumerable();
        if (status != "all")
        {
            filtered = filtered.Where(r => r.Status == status);
        }

        // Resolve user names for display
        var userIds = allRequests
            .SelectMany(r => new[] { r.RequestedBy, r.AssignedTo })
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        ViewBag.UserNames = users;
        ViewBag.CurrentStatus = status;
        ViewBag.TotalCount = allRequests.Count;
        ViewBag.PendingCount = allRequests.Count(r => r.Status == "pending");
        ViewBag.InProgressCount = allRequests.Count(r => r.Status == "in_progress");
        ViewBag.CompletedCount = allRequests.Count(r => r.Status == "completed");
        ViewBag.CancelledCount = allRequests.Count(r => r.Status == "cancelled");

        return View(filtered.ToList());
    }

    [HttpGet]
    public async Task<IActionResult> CreateCleaningRequest()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        await PopulateDropdowns(hotelId.Value);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCleaningRequest(int? roomId, string requestType, string priority, string? assignedTo, string? notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return RedirectToAction("Index", "Home");

        var request = new CleaningRequest
        {
            HotelId = hotelId.Value,
            RoomId = roomId,
            RequestType = string.IsNullOrWhiteSpace(requestType) ? "standard" : requestType.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "normal" : priority.Trim(),
            Status = "pending",
            RequestedBy = currentUser.Id,
            AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.CleaningRequests.Add(request);
        await _db.SaveChangesAsync();

        // Notify the assigned employee
        if (!string.IsNullOrWhiteSpace(request.AssignedTo))
        {
            var room = request.RoomId.HasValue
                ? await _db.Rooms.FindAsync(request.RoomId.Value)
                : null;
            var roomLabel = room != null ? $"Room {room.Number}" : "General area";

            await _notifications.CreateAsync(
                hotelId.Value,
                request.AssignedTo,
                "Cleaning Request Assigned",
                $"You have been assigned a {request.RequestType} cleaning request for {roomLabel}.",
                $"/Housekeeping/EditCleaningRequest/{request.Id}",
                "info");
        }

        TempData["Success"] = "Cleaning request created.";
        return RedirectToAction("CleaningRequests");
    }

    [HttpGet]
    public async Task<IActionResult> EditCleaningRequest(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var request = await _db.CleaningRequests
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);

        if (request == null)
        {
            TempData["Error"] = "Cleaning request not found.";
            return RedirectToAction("CleaningRequests");
        }

        await PopulateDropdowns(hotelId.Value);

        // Resolve requester name
        if (!string.IsNullOrEmpty(request.RequestedBy))
        {
            var requester = await _db.Users.FindAsync(request.RequestedBy);
            ViewBag.RequesterName = requester?.FullName ?? "Unknown";
        }

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCleaningRequest(int id, string status, string? assignedTo, string? notes, string requestType, string priority)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var request = await _db.CleaningRequests
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);

        if (request == null)
        {
            TempData["Error"] = "Cleaning request not found.";
            return RedirectToAction("CleaningRequests");
        }

        var previousAssignedTo = request.AssignedTo;
        var previousStatus = request.Status;

        request.Status = status;
        request.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim();
        request.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        request.RequestType = string.IsNullOrWhiteSpace(requestType) ? request.RequestType : requestType.Trim();
        request.Priority = string.IsNullOrWhiteSpace(priority) ? request.Priority : priority.Trim();

        if (status == "completed" && previousStatus != "completed")
        {
            request.CompletedAt = DateTime.UtcNow;
        }
        else if (status != "completed")
        {
            request.CompletedAt = null;
        }

        await _db.SaveChangesAsync();

        var roomLabel = request.Room != null ? $"Room {request.Room.Number}" : "General area";

        // Notify the assigned employee if assignment changed
        if (!string.IsNullOrWhiteSpace(request.AssignedTo) && request.AssignedTo != previousAssignedTo)
        {
            await _notifications.CreateAsync(
                hotelId.Value,
                request.AssignedTo,
                "Cleaning Request Assigned",
                $"You have been assigned a {request.RequestType} cleaning request for {roomLabel}.",
                $"/Housekeeping/EditCleaningRequest/{request.Id}",
                "info");
        }

        // Notify the requester when completed
        if (status == "completed" && previousStatus != "completed" && !string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            await _notifications.CreateAsync(
                hotelId.Value,
                request.RequestedBy,
                "Cleaning Request Completed",
                $"Your cleaning request for {roomLabel} has been completed.",
                $"/Housekeeping/EditCleaningRequest/{request.Id}",
                "success");
        }

        TempData["Success"] = "Cleaning request updated.";
        return RedirectToAction("CleaningRequests");
    }

    public async Task<IActionResult> Staff()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var hkDepartment = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value &&
                d.Name.ToLower().Contains("housekeeping"));

        if (hkDepartment == null)
        {
            ViewBag.StaffList = new List<dynamic>();
            ViewBag.DepartmentName = "Housekeeping";
            return View();
        }

        var staffMembers = await _db.UserDepartments
            .Where(ud => ud.DepartmentId == hkDepartment.Id)
            .Include(ud => ud.User)
            .Select(ud => new
            {
                ud.User.Id,
                ud.User.FirstName,
                ud.User.LastName,
                ud.User.Email,
                ud.User.Phone,
                ud.User.IsActive,
                ud.IsManager
            })
            .ToListAsync();

        // Get active request counts per staff member
        var staffIds = staffMembers.Select(s => s.Id).ToList();
        var activeRequestCounts = await _db.CleaningRequests
            .Where(r => r.HotelId == hotelId.Value &&
                r.AssignedTo != null &&
                staffIds.Contains(r.AssignedTo) &&
                (r.Status == "pending" || r.Status == "in_progress"))
            .GroupBy(r => r.AssignedTo)
            .ToDictionaryAsync(g => g.Key!, g => g.Count());

        var completedTodayCounts = await _db.CleaningRequests
            .Where(r => r.HotelId == hotelId.Value &&
                r.AssignedTo != null &&
                staffIds.Contains(r.AssignedTo) &&
                r.Status == "completed" &&
                r.CompletedAt != null &&
                r.CompletedAt.Value.Date == DateTime.UtcNow.Date)
            .GroupBy(r => r.AssignedTo)
            .ToDictionaryAsync(g => g.Key!, g => g.Count());

        ViewBag.StaffList = staffMembers;
        ViewBag.ActiveRequestCounts = activeRequestCounts;
        ViewBag.CompletedTodayCounts = completedTodayCounts;
        ViewBag.DepartmentName = hkDepartment.Name;

        return View();
    }

    private async Task PopulateDropdowns(int hotelId)
    {
        // Rooms dropdown
        var rooms = await _db.Rooms
            .Where(r => r.HotelId == hotelId)
            .OrderBy(r => r.Floor).ThenBy(r => r.Number)
            .ToListAsync();
        ViewBag.Rooms = rooms;

        // HK staff dropdown
        var hkDepartment = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId &&
                d.Name.ToLower().Contains("housekeeping"));

        if (hkDepartment != null)
        {
            var staff = await _db.UserDepartments
                .Where(ud => ud.DepartmentId == hkDepartment.Id)
                .Include(ud => ud.User)
                .Where(ud => ud.User.IsActive)
                .Select(ud => new { ud.User.Id, ud.User.FirstName, ud.User.LastName })
                .ToListAsync();
            ViewBag.HkStaff = staff;
        }
        else
        {
            ViewBag.HkStaff = new List<dynamic>();
        }
    }
}
