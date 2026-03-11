namespace MyHotel.Web.Models.Entities;

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Module { get; set; } = "";
}

public class RolePermission
{
    public int Id { get; set; }
    public AppRole Role { get; set; }
    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}

public class UserPermissionOverride
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int HotelId { get; set; }
    public int PermissionId { get; set; }
    public bool IsGranted { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Hotel Hotel { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
