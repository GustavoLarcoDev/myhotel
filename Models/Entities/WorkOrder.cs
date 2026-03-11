namespace MyHotel.Web.Models.Entities;

public class WorkOrder
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string? Room { get; set; }
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "normal";
    public string Status { get; set; } = "pending";
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
