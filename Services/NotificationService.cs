using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Hubs;
using MyHotel.Web.Models.Entities;

namespace MyHotel.Web.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task CreateAsync(int hotelId, string userId, string title, string? message = null, string? link = null, string type = "info")
    {
        var notification = new Notification
        {
            HotelId = hotelId,
            UserId = userId,
            Title = title,
            Message = message,
            Link = link,
            Type = type
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            id = notification.Id,
            title = notification.Title,
            message = notification.Message,
            link = notification.Link,
            type = notification.Type,
            createdAt = notification.CreatedAt.ToString("g")
        });
    }

    public async Task NotifyDepartmentAsync(int hotelId, int departmentId, string title, string? message = null, string? link = null, string type = "info", string? excludeUserId = null)
    {
        var userIds = await _db.UserDepartments
            .Where(ud => ud.Department.HotelId == hotelId && ud.DepartmentId == departmentId)
            .Select(ud => ud.UserId)
            .ToListAsync();

        var notifications = new List<Notification>();
        foreach (var userId in userIds)
        {
            if (userId == excludeUserId) continue;
            var notification = new Notification
            {
                HotelId = hotelId,
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                Type = type
            };
            _db.Notifications.Add(notification);
            notifications.Add(notification);
        }
        await _db.SaveChangesAsync();

        foreach (var notification in notifications)
        {
            await _hubContext.Clients.Group($"user_{notification.UserId}").SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                link = notification.Link,
                type = notification.Type,
                createdAt = notification.CreatedAt.ToString("g")
            });
        }
    }

    public async Task NotifyHotelAsync(int hotelId, string title, string? message = null, string? link = null, string type = "info", string? excludeUserId = null)
    {
        var userIds = await _db.UserHotelRoles
            .Where(r => r.HotelId == hotelId)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        var notifications = new List<Notification>();
        foreach (var userId in userIds)
        {
            if (userId == excludeUserId) continue;
            var notification = new Notification
            {
                HotelId = hotelId,
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                Type = type
            };
            _db.Notifications.Add(notification);
            notifications.Add(notification);
        }
        await _db.SaveChangesAsync();

        foreach (var notification in notifications)
        {
            await _hubContext.Clients.Group($"user_{notification.UserId}").SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                link = notification.Link,
                type = notification.Type,
                createdAt = notification.CreatedAt.ToString("g")
            });
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId, int hotelId)
    {
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && n.HotelId == hotelId && !n.IsRead);
    }

    public async Task<List<Notification>> GetRecentAsync(string userId, int hotelId, int count = 20)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId && n.HotelId == hotelId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId, int hotelId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && n.HotelId == hotelId && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
    }
}
