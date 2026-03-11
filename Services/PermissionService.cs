using Microsoft.EntityFrameworkCore;
using MyHotel.Web.Data;
using MyHotel.Web.Models.Entities;

namespace MyHotel.Web.Services;

public class PermissionService
{
    private readonly ApplicationDbContext _db;

    public PermissionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermission(string userId, int hotelId, string permissionName)
    {
        // Check if user is admin for THIS hotel
        var isAdmin = await _db.UserHotelRoles
            .AnyAsync(r => r.UserId == userId && r.HotelId == hotelId && r.Role == AppRole.Admin);
        if (isAdmin) return true;

        // Check for explicit override
        var overrideEntry = await _db.UserPermissionOverrides
            .Include(o => o.Permission)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.HotelId == hotelId && o.Permission.Name == permissionName);

        if (overrideEntry != null)
            return overrideEntry.IsGranted;

        // Check role-based permissions
        var userRoles = await _db.UserHotelRoles
            .Where(r => r.UserId == userId && r.HotelId == hotelId)
            .Select(r => r.Role)
            .ToListAsync();

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
        if (permission == null) return false;

        // GM and AGM get all permissions by default
        if (userRoles.Contains(AppRole.GeneralManager) || userRoles.Contains(AppRole.AssistantGM))
            return true;

        return await _db.RolePermissions
            .AnyAsync(rp => userRoles.Contains(rp.Role) && rp.PermissionId == permission.Id);
    }

    public async Task<AppRole?> GetUserRole(string userId, int hotelId)
    {
        var role = await _db.UserHotelRoles
            .Where(r => r.UserId == userId && r.HotelId == hotelId)
            .OrderBy(r => r.Role)
            .Select(r => (AppRole?)r.Role)
            .FirstOrDefaultAsync();

        if (role != null) return role;

        // Check admin for any hotel (global admin)
        var isAdmin = await _db.UserHotelRoles
            .AnyAsync(r => r.UserId == userId && r.HotelId == hotelId && r.Role == AppRole.Admin);
        return isAdmin ? AppRole.Admin : null;
    }

    public async Task<List<Permission>> GetAllPermissions()
    {
        return await _db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync();
    }
}
