namespace MyHotel.Web.Models.Entities;

public class PreventiveMaintenance
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Frequency { get; set; } = "monthly";
    public string Status { get; set; } = "scheduled";
    public string? AssignedTo { get; set; }
    public DateTime? LastCompleted { get; set; }
    public DateTime NextDue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
