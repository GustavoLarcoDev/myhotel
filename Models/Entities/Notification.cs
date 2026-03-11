namespace MyHotel.Web.Models.Entities;

public class Notification
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Link { get; set; }
    public string Type { get; set; } = "info"; // info, warning, success, urgent
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
