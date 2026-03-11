using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class WorkOrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkOrdersController(ApplicationDbContext db, HotelContextService hotelContext,
        NotificationService notifications, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _hotelContext = hotelContext;
        _notifications = notifications;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string status = "all", string search = "", string sort = "newest")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var query = _db.WorkOrders.Where(w => w.HotelId == hotelId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(w =>
                (w.Room != null && w.Room.ToLower().Contains(s)) ||
                w.Description.ToLower().Contains(s) ||
                (w.AssignedTo != null && w.AssignedTo.ToLower().Contains(s)) ||
                w.CreatedBy.ToLower().Contains(s));
        }

        if (status != "all")
        {
            query = query.Where(w => w.Status == status);
        }

        query = sort switch
        {
            "oldest" => query.OrderBy(w => w.CreatedAt),
            "priority" => query.OrderByDescending(w =>
                w.Priority == "urgent" ? 4 :
                w.Priority == "high" ? 3 :
                w.Priority == "normal" ? 2 : 1).ThenByDescending(w => w.CreatedAt),
            _ => query.OrderByDescending(w => w.CreatedAt)
        };

        var workOrders = await query.ToListAsync();

        var allOrders = await _db.WorkOrders.Where(w => w.HotelId == hotelId.Value).ToListAsync();
        ViewBag.TotalCount = allOrders.Count;
        ViewBag.PendingCount = allOrders.Count(w => w.Status == "pending");
        ViewBag.InProgressCount = allOrders.Count(w => w.Status == "in-progress");
        ViewBag.CompletedCount = allOrders.Count(w => w.Status == "completed");

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSearch = search;
        ViewBag.CurrentSort = sort;

        return View(workOrders);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string room, string description, string priority, string assignedTo)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "Description is required.";
            return RedirectToAction("Index");
        }

        var user = await _userManager.GetUserAsync(User);

        var workOrder = new WorkOrder
        {
            HotelId = hotelId.Value,
            Room = room?.Trim(),
            Description = description.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "normal" : priority.Trim(),
            AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim(),
            CreatedBy = user?.FullName ?? "Unknown",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrders.Add(workOrder);
        await _db.SaveChangesAsync();

        // Notify engineering/maintenance department about the new work order
        var engDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.HotelId == hotelId.Value &&
                (d.Name.ToLower().Contains("engineering") || d.Name.ToLower().Contains("maintenance")));
        if (engDept != null)
        {
            await _notifications.NotifyDepartmentAsync(
                hotelId.Value, engDept.Id,
                $"New Work Order: {workOrder.Room ?? "General"}",
                workOrder.Description.Length > 50 ? workOrder.Description.Substring(0, 50) + "..." : workOrder.Description,
                "/WorkOrders",
                workOrder.Priority == "urgent" ? "urgent" : "info",
                user?.Id);
        }

        TempData["Success"] = "Work order created.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var workOrder = await _db.WorkOrders.FirstOrDefaultAsync(w => w.Id == id && w.HotelId == hotelId.Value);
        if (workOrder == null)
        {
            TempData["Error"] = "Work order not found.";
            return RedirectToAction("Index");
        }

        workOrder.Status = status;
        if (status == "completed")
        {
            workOrder.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            workOrder.CompletedAt = null;
        }

        await _db.SaveChangesAsync();

        // Notify GMs when a work order status changes to completed or other significant statuses
        if (status == "completed" || status == "in-progress")
        {
            var user = await _userManager.GetUserAsync(User);
            var gmIds = await _db.UserHotelRoles
                .Where(r => r.HotelId == hotelId.Value && (r.Role == AppRole.GeneralManager || r.Role == AppRole.AssistantGM))
                .Select(r => r.UserId).Distinct().ToListAsync();
            foreach (var gmId in gmIds)
            {
                if (gmId == user?.Id) continue;
                await _notifications.CreateAsync(hotelId.Value, gmId,
                    $"Work Order {status}: {workOrder.Room ?? "General"}",
                    workOrder.Description.Length > 50 ? workOrder.Description.Substring(0, 50) + "..." : workOrder.Description,
                    "/WorkOrders", status == "completed" ? "success" : "info");
            }
        }

        TempData["Success"] = "Status updated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddNote(int id, string notes)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var workOrder = await _db.WorkOrders.FirstOrDefaultAsync(w => w.Id == id && w.HotelId == hotelId.Value);
        if (workOrder == null)
        {
            TempData["Error"] = "Work order not found.";
            return RedirectToAction("Index");
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            var timestamp = DateTime.UtcNow.ToString("MM/dd HH:mm");
            var newNote = $"[{timestamp}] {notes.Trim()}";
            workOrder.Notes = string.IsNullOrWhiteSpace(workOrder.Notes)
                ? newNote
                : workOrder.Notes + "\n" + newNote;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Note added.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var workOrder = await _db.WorkOrders.FirstOrDefaultAsync(w => w.Id == id && w.HotelId == hotelId.Value);
        if (workOrder == null)
        {
            TempData["Error"] = "Work order not found.";
            return RedirectToAction("Index");
        }

        _db.WorkOrders.Remove(workOrder);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work order deleted.";
        return RedirectToAction("Index");
    }
}
