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

    public async Task<IActionResult> Index(string tab = "chats", string? userId = null, string? dept = null)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToAction("Index", "Home");

        var currentUser = await _userManager.GetUserAsync(User);

        // Check if GM or AGM for announcement permissions
        var isGMOrAGM = await _db.UserHotelRoles
            .AnyAsync(r => r.UserId == currentUserId && r.HotelId == hotelId.Value
                && (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM));

        // Hotel users for "new chat" picker
        var hotelUsers = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value && r.UserId != currentUserId)
            .Include(r => r.User)
            .Select(r => r.User)
            .Distinct()
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();

        // Departments for announcement targeting
        var departments = await _db.Departments
            .Where(d => d.HotelId == hotelId.Value)
            .OrderBy(d => d.Name)
            .ToListAsync();

        // Unread DM count
        var unreadDMCount = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && m.ToUserId == currentUserId && !m.IsRead && !m.IsAnnouncement)
            .CountAsync();

        // Unread announcement count
        var readAnnouncementIds = await _db.AnnouncementReadReceipts
            .Where(r => r.UserId == currentUserId)
            .Select(r => r.MessageId)
            .ToListAsync();

        var userDeptIds = await _db.UserDepartments
            .Where(ud => ud.UserId == currentUserId)
            .Select(ud => ud.DepartmentId)
            .ToListAsync();

        var unreadAnnouncementCount = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && m.IsAnnouncement && m.FromUserId != currentUserId
                && !readAnnouncementIds.Contains(m.Id))
            .Where(m => !m.AnnouncementDepartments.Any() || m.AnnouncementDepartments.Any(ad => userDeptIds.Contains(ad.DepartmentId)))
            .CountAsync();

        ViewBag.CurrentUserId = currentUserId;
        ViewBag.CurrentUserName = currentUser?.FullName ?? "Unknown";
        ViewBag.IsGMOrAGM = isGMOrAGM;
        ViewBag.HotelUsers = hotelUsers;
        ViewBag.Departments = departments;
        ViewBag.Tab = tab;
        ViewBag.PreselectedUserId = userId;
        ViewBag.Dept = dept;
        ViewBag.UnreadDMCount = unreadDMCount;
        ViewBag.UnreadAnnouncementCount = unreadAnnouncementCount;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetConversationList()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Json(new List<object>());

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Json(new List<object>());

        // Get all DMs involving current user
        var myMessages = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && !m.IsAnnouncement
                && (m.FromUserId == currentUserId || m.ToUserId == currentUserId))
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        // Group by conversation partner
        var conversations = myMessages
            .GroupBy(m => m.FromUserId == currentUserId ? m.ToUserId : m.FromUserId)
            .Select(g =>
            {
                var lastMsg = g.First();
                var partnerId = g.Key;
                var partner = lastMsg.FromUserId == currentUserId ? lastMsg.ToUser : lastMsg.FromUser;
                var unread = g.Count(m => m.ToUserId == currentUserId && !m.IsRead);
                return new
                {
                    userId = partnerId,
                    fullName = partner?.FullName ?? "Unknown",
                    initials = (partner?.FirstName?.Substring(0, 1) ?? "") + (partner?.LastName?.Substring(0, 1) ?? ""),
                    lastMessage = lastMsg.Body.Length > 60 ? lastMsg.Body.Substring(0, 60) + "..." : lastMsg.Body,
                    lastMessageAt = lastMsg.CreatedAt,
                    isMine = lastMsg.FromUserId == currentUserId,
                    unreadCount = unread
                };
            })
            .OrderByDescending(c => c.lastMessageAt)
            .ToList();

        return Json(conversations);
    }

    [HttpGet]
    public async Task<IActionResult> GetConversation(string userId)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Json(new List<object>());

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Json(new List<object>());

        var messages = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && !m.IsAnnouncement
                && ((m.FromUserId == currentUserId && m.ToUserId == userId)
                    || (m.FromUserId == userId && m.ToUserId == currentUserId)))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                body = m.Body,
                fromUserId = m.FromUserId,
                createdAt = m.CreatedAt
            })
            .ToListAsync();

        return Json(messages);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return BadRequest();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return BadRequest();

        if (string.IsNullOrWhiteSpace(request.Body) || string.IsNullOrWhiteSpace(request.ToUserId))
            return BadRequest(new { error = "Message and recipient required." });

        var message = new Message
        {
            HotelId = hotelId.Value,
            Body = request.Body.Trim(),
            FromUserId = currentUserId,
            ToUserId = request.ToUserId,
            IsAnnouncement = false,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        // Notify recipient
        var sender = await _userManager.GetUserAsync(User);
        await _notificationService.CreateAsync(
            hotelId.Value,
            request.ToUserId,
            $"Message from {sender?.FullName ?? "Someone"}",
            message.Body.Length > 50 ? message.Body.Substring(0, 50) + "..." : message.Body,
            $"/Messaging?userId={currentUserId}",
            "info");

        return Json(new
        {
            id = message.Id,
            body = message.Body,
            fromUserId = message.FromUserId,
            createdAt = message.CreatedAt
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MarkConversationRead([FromBody] MarkReadRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (hotelId == null || currentUserId == null) return BadRequest();

        var unreadMessages = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && m.FromUserId == request.UserId
                && m.ToUserId == currentUserId && !m.IsRead && !m.IsAnnouncement)
            .ToListAsync();

        foreach (var m in unreadMessages)
            m.IsRead = true;

        await _db.SaveChangesAsync();
        return Json(new { success = true, markedCount = unreadMessages.Count });
    }

    [HttpGet]
    public async Task<IActionResult> GetAnnouncements()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (hotelId == null || currentUserId == null) return Json(new List<object>());

        var userDeptIds = await _db.UserDepartments
            .Where(ud => ud.UserId == currentUserId)
            .Select(ud => ud.DepartmentId)
            .ToListAsync();

        var readIds = await _db.AnnouncementReadReceipts
            .Where(r => r.UserId == currentUserId)
            .Select(r => r.MessageId)
            .ToListAsync();

        var announcements = await _db.Messages
            .Where(m => m.HotelId == hotelId.Value && m.IsAnnouncement)
            .Where(m => !m.AnnouncementDepartments.Any() || m.AnnouncementDepartments.Any(ad => userDeptIds.Contains(ad.DepartmentId)))
            .Include(m => m.FromUser)
            .Include(m => m.AnnouncementDepartments).ThenInclude(ad => ad.Department)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var result = announcements.Select(a => new
        {
            id = a.Id,
            subject = a.Subject,
            body = a.Body,
            fromName = a.FromUser?.FullName ?? "Unknown",
            fromInitials = (a.FromUser?.FirstName?.Substring(0, 1) ?? "") + (a.FromUser?.LastName?.Substring(0, 1) ?? ""),
            createdAt = a.CreatedAt,
            isRead = readIds.Contains(a.Id),
            departments = a.AnnouncementDepartments.Select(ad => ad.Department.Name).ToList(),
            isAllHotel = !a.AnnouncementDepartments.Any()
        });

        return Json(result);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendAnnouncement([FromBody] SendAnnouncementRequest request)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (hotelId == null || currentUserId == null) return BadRequest();

        // Only GM or AGM can send announcements
        var isGMOrAGM = await _db.UserHotelRoles
            .AnyAsync(r => r.UserId == currentUserId && r.HotelId == hotelId.Value
                && (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM));

        if (!isGMOrAGM)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Subject and body required." });

        var announcement = new Message
        {
            HotelId = hotelId.Value,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            FromUserId = currentUserId,
            IsAnnouncement = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Messages.Add(announcement);
        await _db.SaveChangesAsync();

        // Add department targets
        if (request.DepartmentIds != null && request.DepartmentIds.Any())
        {
            foreach (var deptId in request.DepartmentIds)
            {
                _db.AnnouncementDepartments.Add(new AnnouncementDepartment
                {
                    MessageId = announcement.Id,
                    DepartmentId = deptId
                });
            }
            await _db.SaveChangesAsync();
        }

        // Notify
        var sender = await _userManager.GetUserAsync(User);
        if (request.DepartmentIds != null && request.DepartmentIds.Any())
        {
            foreach (var deptId in request.DepartmentIds)
            {
                await _notificationService.NotifyDepartmentAsync(
                    hotelId.Value, deptId,
                    $"Announcement from {sender?.FullName}",
                    request.Subject,
                    "/Messaging?tab=announcements",
                    "info",
                    currentUserId);
            }
        }
        else
        {
            await _notificationService.NotifyHotelAsync(
                hotelId.Value,
                $"Announcement from {sender?.FullName}",
                request.Subject,
                "/Messaging?tab=announcements",
                "info",
                currentUserId);
        }

        return Json(new { success = true, id = announcement.Id });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MarkAnnouncementRead([FromBody] MarkAnnouncementReadRequest request)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return BadRequest();

        var exists = await _db.AnnouncementReadReceipts
            .AnyAsync(r => r.MessageId == request.MessageId && r.UserId == currentUserId);

        if (!exists)
        {
            _db.AnnouncementReadReceipts.Add(new AnnouncementReadReceipt
            {
                MessageId = request.MessageId,
                UserId = currentUserId,
                ReadAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    // Legacy route support — redirect old links
    [HttpPost]
    public async Task<IActionResult> Send(string? toUserId, string subject, string body, bool isAnnouncement = false, string? dept = null)
    {
        // Redirect to new system
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id, string? dept = null)
    {
        return RedirectToAction("Index");
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
        if (message != null)
        {
            _db.Messages.Remove(message);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }
}

public class SendMessageRequest
{
    public string ToUserId { get; set; } = "";
    public string Body { get; set; } = "";
}

public class MarkReadRequest
{
    public string UserId { get; set; } = "";
}

public class SendAnnouncementRequest
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public List<int>? DepartmentIds { get; set; }
}

public class MarkAnnouncementReadRequest
{
    public int MessageId { get; set; }
}
