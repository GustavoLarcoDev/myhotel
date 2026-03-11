namespace MyHotel.Web.Models.Entities;

public class Hotel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<UserHotelRole> UserRoles { get; set; } = new List<UserHotelRole>();
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
