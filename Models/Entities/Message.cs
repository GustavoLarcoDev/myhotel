namespace MyHotel.Web.Models.Entities;

public class Message
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public string? ToUserId { get; set; }
    public bool IsAnnouncement { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ApplicationUser FromUser { get; set; } = null!;
    public ApplicationUser? ToUser { get; set; }
}
