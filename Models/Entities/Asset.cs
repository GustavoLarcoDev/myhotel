namespace MyHotel.Web.Models.Entities;

public class Asset
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string Condition { get; set; } = "good";
    public DateTime? WarrantyExpiry { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
