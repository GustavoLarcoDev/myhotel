using Microsoft.AspNetCore.Identity;

namespace MyHotel.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserHotelRole> HotelRoles { get; set; } = new List<UserHotelRole>();
    public ICollection<UserDepartment> Departments { get; set; } = new List<UserDepartment>();

    public string FullName => $"{FirstName} {LastName}";
}
