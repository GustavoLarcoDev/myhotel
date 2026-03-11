namespace MyHotel.Web.Models.Entities;

public class GroupSale
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string GroupName { get; set; } = "";
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int RoomsBlocked { get; set; }
    public decimal Revenue { get; set; }
    public string Status { get; set; } = "tentative"; // tentative, confirmed, cancelled, completed
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
