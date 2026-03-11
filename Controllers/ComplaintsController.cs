using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class ComplaintsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public ComplaintsController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string status = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var allComplaints = await _db.Complaints
            .Where(c => c.HotelId == hotelId.Value)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var filtered = status switch
        {
            "open" => allComplaints.Where(c => c.Status == "open").ToList(),
            "in-progress" => allComplaints.Where(c => c.Status == "in-progress").ToList(),
            "resolved" => allComplaints.Where(c => c.Status == "resolved").ToList(),
            "escalated" => allComplaints.Where(c => c.IsEscalated).ToList(),
            _ => allComplaints
        };

        ViewBag.TotalCount = allComplaints.Count;
        ViewBag.OpenCount = allComplaints.Count(c => c.Status == "open");
        ViewBag.InProgressCount = allComplaints.Count(c => c.Status == "in-progress");
        ViewBag.ResolvedCount = allComplaints.Count(c => c.Status == "resolved");
        ViewBag.EscalatedCount = allComplaints.Count(c => c.IsEscalated);

        // Calculate average resolution time
        var resolved = allComplaints.Where(c => c.ResolvedAt != null).ToList();
        if (resolved.Any())
        {
            var avgHours = resolved.Average(c => (c.ResolvedAt!.Value - c.CreatedAt).TotalHours);
            ViewBag.AvgResolutionTime = avgHours < 1
                ? $"{(int)(avgHours * 60)}m"
                : avgHours < 24
                    ? $"{avgHours:F1}h"
                    : $"{(avgHours / 24):F1}d";
        }
        else
        {
            ViewBag.AvgResolutionTime = "N/A";
        }

        ViewBag.CurrentStatus = status;

        return View(filtered);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string guestName, string room, string description)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(guestName) || string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "Guest name and description are required.";
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);

        var complaint = new Complaint
        {
            HotelId = hotelId.Value,
            GuestName = guestName.Trim(),
            Room = string.IsNullOrWhiteSpace(room) ? null : room.Trim(),
            Description = description.Trim(),
            Status = "open",
            CreatedBy = user?.FullName ?? "Unknown",
            CreatedAt = DateTime.UtcNow
        };

        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync();
        var gmUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value &&
                        (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var gmId in gmUserIds)
        {
            if (gmId == user?.Id) continue;
            await _notifications.CreateAsync(
                hotelId.Value,
                gmId,
                $"New Complaint: {complaint.GuestName}",
                complaint.Description.Length > 50 ? complaint.Description.Substring(0, 50) + "..." : complaint.Description,
                "/Complaints",
                "warning");
        }

        TempData["Success"] = "Complaint filed.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int id, string status, string assignedTo, string resolution, int? satisfaction, string compensationNotes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);
        if (complaint == null)
        {
            TempData["Error"] = "Complaint not found.";
            return RedirectToAction("Index");
        }

        if (!string.IsNullOrWhiteSpace(status))
            complaint.Status = status;

        if (!string.IsNullOrWhiteSpace(assignedTo))
            complaint.AssignedTo = assignedTo.Trim();

        if (!string.IsNullOrWhiteSpace(resolution))
            complaint.Resolution = resolution.Trim();

        if (satisfaction.HasValue && satisfaction.Value >= 1 && satisfaction.Value <= 5)
            complaint.Satisfaction = satisfaction.Value;

        if (compensationNotes != null)
            complaint.CompensationNotes = string.IsNullOrWhiteSpace(compensationNotes) ? null : compensationNotes.Trim();

        if (status == "resolved" && complaint.ResolvedAt == null)
            complaint.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Notify GMs about the status change
        var user = await _userManager.GetUserAsync(User);
        var gmUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value &&
                        (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var gmId in gmUserIds)
        {
            if (gmId == user?.Id) continue;
            await _notifications.CreateAsync(
                hotelId.Value,
                gmId,
                $"Complaint Updated: {complaint.GuestName}",
                $"Status: {complaint.Status}",
                "/Complaints",
                "info");
        }

        TempData["Success"] = "Complaint updated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Escalate(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);
        if (complaint == null)
        {
            TempData["Error"] = "Complaint not found.";
            return RedirectToAction("Index");
        }

        complaint.IsEscalated = !complaint.IsEscalated;
        await _db.SaveChangesAsync();
        TempData["Success"] = complaint.IsEscalated ? "Complaint escalated." : "Escalation removed.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == id && c.HotelId == hotelId.Value);
        if (complaint == null)
        {
            TempData["Error"] = "Complaint not found.";
            return RedirectToAction("Index");
        }

        _db.Complaints.Remove(complaint);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Complaint deleted.";
        return RedirectToAction("Index");
    }
}
