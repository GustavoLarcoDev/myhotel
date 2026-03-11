using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;

namespace MyHotel.Web.Services;

public class HotelContextService
{
    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;

    public HotelContextService(IHttpContextAccessor http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    public int? CurrentHotelId
    {
        get
        {
            var session = _http.HttpContext?.Session;
            if (session == null) return null;
            var id = session.GetInt32("CurrentHotelId");
            return id;
        }
    }

    public async Task SetCurrentHotel(int hotelId)
    {
        var session = _http.HttpContext?.Session;
        if (session != null)
        {
            session.SetInt32("CurrentHotelId", hotelId);
        }
    }

    public async Task<Hotel?> GetCurrentHotel()
    {
        var id = CurrentHotelId;
        if (id == null) return null;
        return await _db.Hotels.FindAsync(id);
    }

    public async Task<List<Hotel>> GetUserHotels(string userId)
    {
        return await _db.UserHotelRoles
            .Where(r => r.UserId == userId)
            .Select(r => r.Hotel)
            .Distinct()
            .ToListAsync();
    }
}
