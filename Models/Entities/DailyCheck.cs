namespace MyHotel.Web.Models.Entities;

public class DailyCheck
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string CheckItem { get; set; } = "";
    public string Category { get; set; } = "general";
    public bool IsCompleted { get; set; }
    public string? CompletedBy { get; set; }
    public string? Notes { get; set; }
    public string Date { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}

public class InspectionTemplate
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "general";
    public string CheckItem { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
