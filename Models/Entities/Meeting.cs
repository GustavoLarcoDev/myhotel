namespace MyHotel.Web.Models.Entities;

public class Meeting
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public int? DepartmentId { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public Department? Department { get; set; }
    public ICollection<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();
}

public class MeetingAttendee
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public string UserId { get; set; } = "";
    public bool Attended { get; set; }

    public Meeting Meeting { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
