namespace MyHotel.Web.Models.Entities;

public class LostFound
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string ItemDescription { get; set; } = "";
    public string? Location { get; set; }
    public string Status { get; set; } = "found";
    public string? FoundBy { get; set; }
    public string? ClaimedBy { get; set; }
    public string? GuestName { get; set; }
    public string? StorageLocation { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
