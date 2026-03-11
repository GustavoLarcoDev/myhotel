namespace MyHotel.Web.Models.Entities;

public class Vendor
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Name { get; set; } = "";
    public string? Service { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
