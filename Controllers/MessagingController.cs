using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class MessagingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MessagingController(
        ApplicationDbContext db,
        HotelContextService hotelContext,
        NotificationService notificationService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string tab = "inbox", string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToAction("Index", "Home");

        if (tab != "inbox" && tab != "sent" && tab != "announcements")
            tab = "inbox";

        List<Message> messages;

        switch (tab)
        {
            case "sent":
                messages = await _db.Messages
                    .Where(m => m.HotelId == hotelId.Value && m.FromUserId == currentUserId)
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();
                break;
            case "announcements":
                messages = await _db.Messages
                    .Where(m => m.HotelId == hotelId.Value && m.IsAnnouncement)
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();
                break;
            default: // inbox
                messages = await _db.Messages
                    .Where(m => m.HotelId == hotelId.Value && m.ToUserId == currentUserId && !m.IsAnnouncement)
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();
                break;
        }

        // Unread count (inbox only)
        var unreadCount = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && m.ToUserId == currentUserId && !m.IsRead && !m.IsAnnouncement)
            .CountAsync();

        // Hotel users for recipient dropdown
        List<ApplicationUser> hotelUsers;

        if (!string.IsNullOrWhiteSpace(dept))
        {
            // Build department name search patterns based on dept parameter
            var deptPatterns = GetDeptPatterns(dept);

            if (deptPatterns.Count > 0)
            {
                // Find matching department IDs for this hotel
                var departmentsQuery = _db.Set<Department>()
                    .Where(d => d.HotelId == hotelId.Value);

                // Build OR filter for department name patterns
                var matchingDeptIds = await departmentsQuery
                    .Where(d => deptPatterns.Any(p => d.Name.ToLower().Contains(p)))
                    .Select(d => d.Id)
                    .ToListAsync();

                if (matchingDeptIds.Count > 0)
                {
                    // Get user IDs in those departments
                    var deptUserIds = await _db.UserDepartments
                        .Where(ud => matchingDeptIds.Contains(ud.DepartmentId) && ud.UserId != currentUserId)
                        .Select(ud => ud.UserId)
                        .Distinct()
                        .ToListAsync();

                    // Get user objects that are also in this hotel
                    hotelUsers = await _db.UserHotelRoles
                        .Where(r => r.HotelId == hotelId.Value && deptUserIds.Contains(r.UserId))
                        .Include(r => r.User)
                        .Select(r => r.User)
                        .Distinct()
                        .OrderBy(u => u.FirstName)
                        .ThenBy(u => u.LastName)
                        .ToListAsync();
                }
                else
                {
                    // No matching departments found, show all hotel users
                    hotelUsers = await GetAllHotelUsers(hotelId.Value, currentUserId);
                }
            }
            else
            {
                // Unknown dept value, show all hotel users
                hotelUsers = await GetAllHotelUsers(hotelId.Value, currentUserId);
            }
        }
        else
        {
            hotelUsers = await GetAllHotelUsers(hotelId.Value, currentUserId);
        }

        ViewBag.CurrentTab = tab;
        ViewBag.UnreadCount = unreadCount;
        ViewBag.HotelUsers = hotelUsers;
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentDept = dept;
        ViewBag.DeptName = GetDeptDisplayName(dept);

        return View(messages);
    }

    [HttpPost]
    public async Task<IActionResult> Send(string? toUserId, string subject, string body, bool isAnnouncement = false, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToAction("Index", "Home");

        var redirectRoute = new { tab = "sent", dept };

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Subject and body are required.";
            return RedirectToAction("Index", new { dept });
        }

        if (!isAnnouncement && string.IsNullOrWhiteSpace(toUserId))
        {
            TempData["Error"] = "Please select a recipient.";
            return RedirectToAction("Index", new { dept });
        }

        var message = new Message
        {
            HotelId = hotelId.Value,
            Subject = subject.Trim(),
            Body = body.Trim(),
            FromUserId = currentUserId,
            ToUserId = isAnnouncement ? null : toUserId,
            IsAnnouncement = isAnnouncement,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        // Send notifications
        var sender = await _userManager.FindByIdAsync(currentUserId);
        var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}".Trim() : "Someone";
        var deptParam = !string.IsNullOrWhiteSpace(dept) ? $"?dept={dept}" : "";

        if (isAnnouncement)
        {
            // Notify all hotel users about the announcement
            await _notificationService.NotifyHotelAsync(
                hotelId.Value,
                $"New Announcement from {senderName}",
                message: subject.Trim(),
                link: $"/Messaging?tab=announcements{(string.IsNullOrWhiteSpace(dept) ? "" : $"&dept={dept}")}",
                type: "info",
                excludeUserId: currentUserId);
        }
        else if (!string.IsNullOrWhiteSpace(toUserId))
        {
            // Notify the recipient about the direct message
            await _notificationService.CreateAsync(
                hotelId.Value,
                toUserId,
                $"New Message from {senderName}",
                message: subject.Trim(),
                link: $"/Messaging{deptParam}",
                type: "info");
        }

        TempData["Success"] = isAnnouncement ? "Announcement posted." : "Message sent.";
        return RedirectToAction("Index", redirectRoute);
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == id && m.HotelId == hotelId.Value && m.ToUserId == currentUserId);
        if (message == null)
        {
            TempData["Error"] = "Message not found.";
            return RedirectToAction("Index", new { dept });
        }

        message.IsRead = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message marked as read.";
        return RedirectToAction("Index", new { dept });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == id && m.HotelId == hotelId.Value &&
                (m.FromUserId == currentUserId || m.ToUserId == currentUserId));
        if (message == null)
        {
            TempData["Error"] = "Message not found.";
            return RedirectToAction("Index", new { dept });
        }

        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message deleted.";
        return RedirectToAction("Index", new { dept });
    }

    private async Task<List<ApplicationUser>> GetAllHotelUsers(int hotelId, string currentUserId)
    {
        return await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId && r.UserId != currentUserId)
            .Include(r => r.User)
            .Select(r => r.User)
            .Distinct()
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    private static List<string> GetDeptPatterns(string? dept)
    {
        return dept?.ToLower() switch
        {
            "sales" => new List<string> { "sales" },
            "engineering" => new List<string> { "engineering", "maintenance" },
            "frontdesk" => new List<string> { "front desk" },
            "housekeeping" => new List<string> { "housekeeping" },
            "administrative" => new List<string> { "administrative" },
            _ => new List<string>()
        };
    }

    private static string? GetDeptDisplayName(string? dept)
    {
        return dept?.ToLower() switch
        {
            "sales" => "Sales",
            "engineering" => "Engineering",
            "frontdesk" => "Front Desk",
            "housekeeping" => "Housekeeping",
            "administrative" => "Administrative",
            _ => null
        };
    }
}
