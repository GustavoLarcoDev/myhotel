using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;
using MyHotel.Web.Services;

namespace MyHotel.Web.Controllers;

[Authorize]
public class RoomsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly HotelContextService _hotelContext;

    public RoomsController(ApplicationDbContext db, HotelContextService hotelContext)
    {
        _db = db;
        _hotelContext = hotelContext;
    }

    public async Task<IActionResult> Index(string floor = "all", string status = "all")
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var allRooms = await _db.Rooms
            .Where(r => r.HotelId == hotelId.Value)
            .Include(r => r.Notices)
            .OrderBy(r => r.Floor).ThenBy(r => r.Number)
            .ToListAsync();

        var filtered = allRooms.AsEnumerable();

        if (floor != "all" && int.TryParse(floor, out var floorNum))
        {
            filtered = filtered.Where(r => r.Floor == floorNum);
        }

        if (status != "all")
        {
            filtered = filtered.Where(r => r.Status == status);
        }

        var rooms = filtered.ToList();

        ViewBag.AllRooms = allRooms;
        ViewBag.VacantCleanCount = allRooms.Count(r => r.Status == "vacant-clean");
        ViewBag.VacantDirtyCount = allRooms.Count(r => r.Status == "vacant-dirty");
        ViewBag.OccupiedCount = allRooms.Count(r => r.Status == "occupied");
        ViewBag.OutOfOrderCount = allRooms.Count(r => r.Status == "out-of-order");
        ViewBag.InspectedCount = allRooms.Count(r => r.Status == "inspected");
        ViewBag.TotalCount = allRooms.Count;

        ViewBag.Floors = allRooms.Select(r => r.Floor).Distinct().OrderBy(f => f).ToList();
        ViewBag.CurrentFloor = floor;
        ViewBag.CurrentStatus = status;

        return View(rooms);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string number, int floor, string type)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(number))
        {
            TempData["Error"] = "Room number is required.";
            return RedirectToAction("Index");
        }

        var exists = await _db.Rooms.AnyAsync(r => r.HotelId == hotelId.Value && r.Number == number.Trim());
        if (exists)
        {
            TempData["Error"] = $"Room {number.Trim()} already exists.";
            return RedirectToAction("Index");
        }

        var room = new Room
        {
            HotelId = hotelId.Value,
            Number = number.Trim(),
            Floor = floor,
            Type = string.IsNullOrWhiteSpace(type) ? "standard" : type.Trim(),
            Status = "vacant-clean",
            CreatedAt = DateTime.UtcNow
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Room {room.Number} created.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);
        if (room == null)
        {
            TempData["Error"] = "Room not found.";
            return RedirectToAction("Index");
        }

        room.Status = status;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Room {room.Number} status updated to {status}.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddNotice(int roomId, string type, string note)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.HotelId == hotelId.Value);
        if (room == null)
        {
            TempData["Error"] = "Room not found.";
            return RedirectToAction("Index");
        }

        var notice = new RoomNotice
        {
            RoomId = roomId,
            Type = string.IsNullOrWhiteSpace(type) ? "DND" : type.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.RoomNotices.Add(notice);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Notice added to room {room.Number}.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> RemoveNotice(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var notice = await _db.RoomNotices
            .Include(n => n.Room)
            .FirstOrDefaultAsync(n => n.Id == id && n.Room.HotelId == hotelId.Value);

        if (notice == null)
        {
            TempData["Error"] = "Notice not found.";
            return RedirectToAction("Index");
        }

        _db.RoomNotices.Remove(notice);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Notice removed.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var room = await _db.Rooms
            .Include(r => r.Notices)
            .FirstOrDefaultAsync(r => r.Id == id && r.HotelId == hotelId.Value);

        if (room == null)
        {
            TempData["Error"] = "Room not found.";
            return RedirectToAction("Index");
        }

        _db.RoomNotices.RemoveRange(room.Notices);
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Room {room.Number} deleted.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SeedRooms()
    {
        var hotelId = _hotelContext.CurrentHotelId;
        if (hotelId == null) return RedirectToAction("Index", "Home");

        var existingCount = await _db.Rooms.CountAsync(r => r.HotelId == hotelId.Value);
        if (existingCount > 0)
        {
            TempData["Error"] = "Rooms already exist. Delete existing rooms first.";
            return RedirectToAction("Index");
        }

        var types = new[] { "standard", "standard", "standard", "deluxe", "deluxe", "suite", "standard", "accessible", "standard", "deluxe" };
        var rooms = new List<Room>();

        for (int floor = 1; floor <= 4; floor++)
        {
            for (int room = 1; room <= 10; room++)
            {
                var roomNumber = $"{floor}{room:D2}";
                rooms.Add(new Room
                {
                    HotelId = hotelId.Value,
                    Number = roomNumber,
                    Floor = floor,
                    Type = types[room - 1],
                    Status = "vacant-clean",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _db.Rooms.AddRange(rooms);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{rooms.Count} rooms created (floors 1-4, rooms 101-410).";
        return RedirectToAction("Index");
    }
}
