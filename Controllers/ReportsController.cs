using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public ReportsController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    public async Task<IActionResult> Index(string period = "today")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var now = DateTime.UtcNow;
        DateTime? periodStart = period switch
        {
            "today" => now.Date,
            "week" => now.Date.AddDays(-(int)now.DayOfWeek),
            "month" => new DateTime(now.Year, now.Month, 1),
            "all" => null,
            _ => now.Date
        };

        ViewBag.CurrentPeriod = period;

        // ---- Work Orders ----
        var allWorkOrders = await _db.WorkOrders.Where(w => w.HotelId == hotelId.Value).ToListAsync();
        var periodWorkOrders = periodStart.HasValue
            ? allWorkOrders.Where(w => w.CreatedAt >= periodStart.Value).ToList()
            : allWorkOrders;

        ViewBag.WoTotal = periodWorkOrders.Count;
        ViewBag.WoCompleted = periodWorkOrders.Count(w => w.Status == "completed");
        ViewBag.WoCompletionRate = periodWorkOrders.Count > 0
            ? Math.Round((double)periodWorkOrders.Count(w => w.Status == "completed") / periodWorkOrders.Count * 100, 1)
            : 0;
        ViewBag.WoPending = periodWorkOrders.Count(w => w.Status == "pending");
        ViewBag.WoInProgress = periodWorkOrders.Count(w => w.Status == "in-progress");

        var completedWithTime = periodWorkOrders
            .Where(w => w.Status == "completed" && w.CompletedAt.HasValue)
            .Select(w => (w.CompletedAt!.Value - w.CreatedAt).TotalHours)
            .ToList();
        ViewBag.WoAvgResolutionHours = completedWithTime.Any() ? Math.Round(completedWithTime.Average(), 1) : 0;

        ViewBag.WoByPriority = periodWorkOrders
            .GroupBy(w => w.Priority)
            .ToDictionary(g => g.Key, g => g.Count());

        // ---- Complaints ----
        var allComplaints = await _db.Complaints.Where(c => c.HotelId == hotelId.Value).ToListAsync();
        var periodComplaints = periodStart.HasValue
            ? allComplaints.Where(c => c.CreatedAt >= periodStart.Value).ToList()
            : allComplaints;

        ViewBag.ComplaintsTotal = periodComplaints.Count;
        ViewBag.ComplaintsResolved = periodComplaints.Count(c => c.Status == "resolved");
        ViewBag.ComplaintsResolvedRate = periodComplaints.Count > 0
            ? Math.Round((double)periodComplaints.Count(c => c.Status == "resolved") / periodComplaints.Count * 100, 1)
            : 0;
        ViewBag.ComplaintsOpen = periodComplaints.Count(c => c.Status == "open");
        ViewBag.ComplaintsEscalated = periodComplaints.Count(c => c.IsEscalated);
        ViewBag.ComplaintsEscalationRate = periodComplaints.Count > 0
            ? Math.Round((double)periodComplaints.Count(c => c.IsEscalated) / periodComplaints.Count * 100, 1)
            : 0;

        var resolvedWithTime = periodComplaints
            .Where(c => c.Status == "resolved" && c.ResolvedAt.HasValue)
            .Select(c => (c.ResolvedAt!.Value - c.CreatedAt).TotalHours)
            .ToList();
        ViewBag.ComplaintsAvgResolutionHours = resolvedWithTime.Any() ? Math.Round(resolvedWithTime.Average(), 1) : 0;

        ViewBag.ComplaintsByStatus = periodComplaints
            .GroupBy(c => c.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        // ---- Daily Checks ----
        var todayStr = now.ToString("yyyy-MM-dd");
        var allChecks = await _db.DailyChecks.Where(d => d.HotelId == hotelId.Value).ToListAsync();
        var periodChecks = period == "today"
            ? allChecks.Where(d => d.Date == todayStr).ToList()
            : periodStart.HasValue
                ? allChecks.Where(d => string.Compare(d.Date, periodStart.Value.ToString("yyyy-MM-dd")) >= 0).ToList()
                : allChecks;

        ViewBag.ChecksTotal = periodChecks.Count;
        ViewBag.ChecksCompleted = periodChecks.Count(d => d.IsCompleted);
        ViewBag.ChecksCompletionRate = periodChecks.Count > 0
            ? Math.Round((double)periodChecks.Count(d => d.IsCompleted) / periodChecks.Count * 100, 1)
            : 0;

        ViewBag.ChecksByCategory = periodChecks
            .GroupBy(d => d.Category)
            .ToDictionary(
                g => g.Key,
                g => new int[] { g.Count(), g.Count(x => x.IsCompleted) });

        // ---- Logs ----
        var allLogs = await _db.Logs.Where(l => l.HotelId == hotelId.Value).ToListAsync();
        var periodLogs = periodStart.HasValue
            ? allLogs.Where(l => l.CreatedAt >= periodStart.Value).ToList()
            : allLogs;

        ViewBag.LogsCount = periodLogs.Count;
        ViewBag.LogsUnread = periodLogs.Count(l => !l.IsRead);

        ViewBag.LogsByCategory = periodLogs
            .GroupBy(l => l.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // ---- Pass Logs ----
        var allPassLogs = await _db.PassLogs.Where(p => p.HotelId == hotelId.Value).ToListAsync();
        var periodPassLogs = periodStart.HasValue
            ? allPassLogs.Where(p => p.CreatedAt >= periodStart.Value).ToList()
            : allPassLogs;

        ViewBag.PassLogsCount = periodPassLogs.Count;
        ViewBag.PassLogsUnread = periodPassLogs.Count(p => !p.IsRead);

        // ---- Rooms ----
        var rooms = await _db.Rooms.Where(r => r.HotelId == hotelId.Value).ToListAsync();
        ViewBag.RoomsTotal = rooms.Count;
        var occupied = rooms.Count(r => r.Status == "occupied");
        ViewBag.RoomsOccupied = occupied;
        ViewBag.RoomsOccupancyRate = rooms.Count > 0
            ? Math.Round((double)occupied / rooms.Count * 100, 1)
            : 0;

        ViewBag.RoomsByStatus = rooms
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        // ---- Preventive Maintenance ----
        var pms = await _db.PreventiveMaintenances.Where(p => p.HotelId == hotelId.Value).ToListAsync();
        ViewBag.PmTotal = pms.Count;
        ViewBag.PmOverdue = pms.Count(p => p.NextDue < now && p.Status != "completed");
        ViewBag.PmUpcoming = pms.Count(p => p.NextDue >= now && p.NextDue <= now.AddDays(7));
        ViewBag.PmCompleted = pms.Count(p => p.Status == "completed");

        // ---- Lost & Found ----
        var lostFound = await _db.LostFoundItems.Where(l => l.HotelId == hotelId.Value).ToListAsync();
        var periodLostFound = periodStart.HasValue
            ? lostFound.Where(l => l.CreatedAt >= periodStart.Value).ToList()
            : lostFound;
        ViewBag.LostFoundTotal = periodLostFound.Count;
        ViewBag.LostFoundClaimed = periodLostFound.Count(l => l.Status == "claimed");

        // ---- Assets ----
        var assets = await _db.Assets.Where(a => a.HotelId == hotelId.Value).ToListAsync();
        ViewBag.AssetsTotal = assets.Count;
        ViewBag.AssetsWarrantyExpiring = assets.Count(a => a.WarrantyExpiry.HasValue && a.WarrantyExpiry.Value <= now.AddDays(30) && a.WarrantyExpiry.Value >= now);

        // ---- Budget/Expenses ----
        var currentMonth = now.ToString("yyyy-MM");
        var monthExpenses = await _db.Expenses
            .Where(e => e.HotelId == hotelId.Value && e.Date.Year == now.Year && e.Date.Month == now.Month)
            .ToListAsync();
        var monthBudgets = await _db.BudgetItems
            .Where(b => b.HotelId == hotelId.Value && b.Period == currentMonth)
            .ToListAsync();

        ViewBag.ExpensesTotal = monthExpenses.Sum(e => e.Amount);
        ViewBag.BudgetTotal = monthBudgets.Sum(b => b.PlannedAmount);
        ViewBag.BudgetUtilization = monthBudgets.Sum(b => b.PlannedAmount) > 0
            ? Math.Round((double)(monthExpenses.Sum(e => e.Amount) / monthBudgets.Sum(b => b.PlannedAmount)) * 100, 1)
            : 0;

        ViewBag.ExpensesByCategory = monthExpenses
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        // ---- Inventory ----
        var inventoryItems = await _db.InventoryItems.Where(i => i.HotelId == hotelId.Value).ToListAsync();
        ViewBag.InventoryTotal = inventoryItems.Count;
        ViewBag.InventoryLowStock = inventoryItems.Count(i => i.Quantity <= i.MinStock);

        // ---- Meetings ----
        var meetings = await _db.Meetings.Where(m => m.HotelId == hotelId.Value).ToListAsync();
        var periodMeetings = periodStart.HasValue
            ? meetings.Where(m => m.Date >= periodStart.Value).ToList()
            : meetings;
        ViewBag.MeetingsCount = periodMeetings.Count;

        // ---- Evaluations ----
        var evaluations = await _db.Evaluations.Where(e => e.HotelId == hotelId.Value).ToListAsync();
        var periodEvaluations = periodStart.HasValue
            ? evaluations.Where(e => e.CreatedAt >= periodStart.Value).ToList()
            : evaluations;
        ViewBag.EvaluationsCount = periodEvaluations.Count;
        ViewBag.EvaluationsAvgRating = periodEvaluations.Any()
            ? Math.Round(periodEvaluations.Average(e => e.Rating), 1)
            : 0;

        // ---- HK Ratings ----
        var hkRatings = await _db.HousekeepingRatings.Where(h => h.HotelId == hotelId.Value).ToListAsync();
        var periodHkRatings = periodStart.HasValue
            ? hkRatings.Where(h => h.CreatedAt >= periodStart.Value).ToList()
            : hkRatings;
        ViewBag.HkRatingsCount = periodHkRatings.Count;
        ViewBag.HkRatingsAvg = periodHkRatings.Any()
            ? Math.Round(periodHkRatings.Average(h => h.Stars), 1)
            : 0;

        // ---- Schedules ----
        var schedules = await _db.Schedules.Where(s => s.HotelId == hotelId.Value).ToListAsync();
        var periodSchedules = periodStart.HasValue
            ? schedules.Where(s => s.Date >= periodStart.Value).ToList()
            : schedules;
        ViewBag.SchedulesCount = periodSchedules.Count;

        return View();
    }
}
