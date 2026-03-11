using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
[Route("Notifications")]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HotelContextService _hotelContext;
    private readonly NotificationService _notificationService;

    public NotificationsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        HotelContextService hotelContext,
        NotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _hotelContext = hotelContext;
        _notificationService = notificationService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        var notifications = await _notificationService.GetRecentAsync(user.Id, hotelId.Value, 50);
        var unreadCount = await _notificationService.GetUnreadCountAsync(user.Id, hotelId.Value);

        ViewBag.Notifications = notifications;
        ViewBag.UnreadCount = unreadCount;

        return View();
    }

    [HttpPost("MarkRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        await _notificationService.MarkAsReadAsync(id, user.Id);

        return RedirectToAction("Index");
    }

    [HttpPost("MarkAllRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Login", "Account");

        await _notificationService.MarkAllAsReadAsync(user.Id, hotelId.Value);

        return RedirectToAction("Index");
    }

    [HttpGet("GetUnreadCount")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Json(new { count = 0 });

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new { count = 0 });

        var count = await _notificationService.GetUnreadCountAsync(user.Id, hotelId.Value);
        return Json(new { count });
    }

    [HttpGet("Recent")]
    public async Task<IActionResult> Recent()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return Json(new List<object>());

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new List<object>());

        var notifications = await _notificationService.GetRecentAsync(user.Id, hotelId.Value, 10);
        return Json(notifications.Select(n => new {
            n.Id,
            n.Title,
            n.Message,
            n.Link,
            n.Type,
            n.IsRead,
            CreatedAt = n.CreatedAt.ToString("g")
        }));
    }
}
