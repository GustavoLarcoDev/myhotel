namespace MyHotel.Web.Models.Entities;

public class Schedule
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public int DepartmentId { get; set; }
    public string EmployeeId { get; set; } = "";
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ApplicationUser Employee { get; set; } = null!;
}
