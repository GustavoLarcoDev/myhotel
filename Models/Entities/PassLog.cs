namespace MyHotel.Web.Models.Entities;

public class PassLog
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string ShiftFrom { get; set; } = "";
    public string ShiftTo { get; set; } = "";
    public string Message { get; set; } = "";
    public string Priority { get; set; } = "normal";
    public string CreatedBy { get; set; } = "";
    public bool IsRead { get; set; }
    public string? ReadBy { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
