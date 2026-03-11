using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class MeetingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;

    public MeetingsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
    }

    // GET: /Meetings?upcoming=true
    public async Task<IActionResult> Index(bool upcoming = true)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var now = DateTime.UtcNow;

        var meetingsQuery = _db.Meetings
            .Where(m => m.HotelId == hotelId.Value)
            .Include(m => m.Department)
            .Include(m => m.Attendees)
                .ThenInclude(a => a.User)
            .AsQueryable();

        if (upcoming)
        {
            meetingsQuery = meetingsQuery
                .Where(m => m.Date >= now.Date)
                .OrderBy(m => m.Date);
        }
        else
        {
            meetingsQuery = meetingsQuery
                .Where(m => m.Date < now.Date)
                .OrderByDescending(m => m.Date);
        }

        // SQLite doesn't support TimeSpan in ORDER BY, so sort by Time client-side
        var meetings = (await meetingsQuery.ToListAsync())
            .OrderBy(m => upcoming ? m.Date : DateTime.MaxValue)
            .ThenBy(m => upcoming ? m.Time : TimeSpan.Zero)
            .ToList();
        if (!upcoming)
            meetings = meetings.OrderByDescending(m => m.Date).ThenByDescending(m => m.Time).ToList();

        // Get departments for the create form
        var departments = await _db.Departments
            .Where(d => d.HotelId == hotelId.Value)
            .OrderBy(d => d.Name)
            .ToListAsync();

        // Get all hotel employees for attendee selection
        var hotelUserIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId.Value)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        var employees = await _db.Users
            .Where(u => hotelUserIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        // Get department memberships for filtering
        var userDepartments = await _db.UserDepartments
            .Where(ud => hotelUserIds.Contains(ud.UserId))
            .ToListAsync();

        ViewBag.Upcoming = upcoming;
        ViewBag.Departments = departments;
        ViewBag.Employees = employees;
        ViewBag.UserDepartments = userDepartments;

        return View(meetings);
    }

    // POST: /Meetings/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string? description, DateTime date, TimeSpan time, int? departmentId, string[]? attendeeIds)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Meeting title is required.";
            return RedirectToAction("Index");
        }

        var meeting = new Meeting
        {
            HotelId = hotelId.Value,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Date = date.Date,
            Time = time,
            DepartmentId = departmentId == 0 ? null : departmentId,
            CreatedBy = user.FullName,
            CreatedAt = DateTime.UtcNow
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        // Add attendees
        if (attendeeIds != null && attendeeIds.Length > 0)
        {
            foreach (var attendeeId in attendeeIds)
            {
                _db.MeetingAttendees.Add(new MeetingAttendee
                {
                    MeetingId = meeting.Id,
                    UserId = attendeeId,
                    Attended = false
                });
            }
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Meeting created.";
        return RedirectToAction("Index");
    }

    // POST: /Meetings/AddNotes
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNotes(int id, string notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == id && m.HotelId == hotelId.Value);

        if (meeting == null)
        {
            TempData["Error"] = "Meeting not found.";
            return RedirectToAction("Index");
        }

        meeting.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await _db.SaveChangesAsync();
        TempData["Success"] = "Notes updated.";
        return RedirectToAction("Index");
    }

    // POST: /Meetings/ToggleAttendance
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAttendance(int attendeeId)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var attendee = await _db.MeetingAttendees
            .Include(a => a.Meeting)
            .FirstOrDefaultAsync(a => a.Id == attendeeId && a.Meeting.HotelId == hotelId.Value);

        if (attendee == null)
        {
            TempData["Error"] = "Attendee not found.";
            return RedirectToAction("Index");
        }

        attendee.Attended = !attendee.Attended;
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    // POST: /Meetings/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var meeting = await _db.Meetings
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == id && m.HotelId == hotelId.Value);

        if (meeting == null)
        {
            TempData["Error"] = "Meeting not found.";
            return RedirectToAction("Index");
        }

        _db.MeetingAttendees.RemoveRange(meeting.Attendees);
        _db.Meetings.Remove(meeting);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Meeting deleted.";
        return RedirectToAction("Index");
    }
}
