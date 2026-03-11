namespace MyHotel.Web.Models.Entities;

public class Reading
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Type { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public string? Location { get; set; }
    public string RecordedBy { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
