using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    public MessagingController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    public async Task<IActionResult> Index(string tab = "inbox")
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
        var hotelUsers = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value && r.UserId != currentUserId)
            .Include(r => r.User)
            .Select(r => r.User)
            .Distinct()
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        ViewBag.CurrentTab = tab;
        ViewBag.UnreadCount = unreadCount;
        ViewBag.HotelUsers = hotelUsers;
        ViewBag.CurrentUserId = currentUserId;

        return View(messages);
    }

    [HttpPost]
    public async Task<IActionResult> Send(string? toUserId, string subject, string body, bool isAnnouncement = false)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Subject and body are required.";
            return RedirectToAction("Index");
        }

        if (!isAnnouncement && string.IsNullOrWhiteSpace(toUserId))
        {
            TempData["Error"] = "Please select a recipient.";
            return RedirectToAction("Index");
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
        TempData["Success"] = isAnnouncement ? "Announcement posted." : "Message sent.";
        return RedirectToAction("Index", new { tab = "sent" });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == id && m.HotelId == hotelId.Value && m.ToUserId == currentUserId);
        if (message == null)
        {
            TempData["Error"] = "Message not found.";
            return RedirectToAction("Index");
        }

        message.IsRead = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message marked as read.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
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
            return RedirectToAction("Index");
        }

        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message deleted.";
        return RedirectToAction("Index");
    }
}
