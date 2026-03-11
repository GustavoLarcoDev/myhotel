namespace MyHotel.Web.Models.Entities;

public class Log
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Category { get; set; } = "general";
    public string Message { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public bool IsRead { get; set; }
    public string? ReadBy { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
