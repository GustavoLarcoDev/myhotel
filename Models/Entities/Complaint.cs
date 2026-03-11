namespace MyHotel.Web.Models.Entities;

public class Complaint
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string GuestName { get; set; } = "";
    public string? Room { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "open";
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    public int? Satisfaction { get; set; }
    public bool IsEscalated { get; set; }
    public string? CompensationNotes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
