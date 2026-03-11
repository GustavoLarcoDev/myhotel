namespace MyHotel.Web.Models.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int HotelId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Hotel Hotel { get; set; } = null!;
    public ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();
}

public class UserDepartment
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int DepartmentId { get; set; }
    public bool IsManager { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public Department Department { get; set; } = null!;
}
