namespace MyHotel.Web.Models.Entities;

public class CleaningRequest
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public int? RoomId { get; set; }
    public string RequestType { get; set; } = "standard"; // standard, deep, turnover, rush, inspection
    public string Priority { get; set; } = "normal"; // low, normal, high, urgent
    public string Status { get; set; } = "pending"; // pending, in_progress, completed, cancelled
    public string? RequestedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Hotel Hotel { get; set; } = null!;
    public Room? Room { get; set; }
}
