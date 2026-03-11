namespace MyHotel.Web.Models.Entities;

public enum AppRole
{
    Admin = 0,
    GeneralManager = 1,
    AssistantGM = 2,
    DepartmentManager = 3,
    Employee = 4
}

public class UserHotelRole
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int HotelId { get; set; }
    public AppRole Role { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public Hotel Hotel { get; set; } = null!;
}
