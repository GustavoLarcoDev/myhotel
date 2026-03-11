namespace MyHotel.Web.Models.Entities;

public class Room
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Number { get; set; } = "";
    public int Floor { get; set; }
    public string Type { get; set; } = "standard";
    public string Status { get; set; } = "vacant-clean";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ICollection<RoomNotice> Notices { get; set; } = new List<RoomNotice>();
}

public class RoomNotice
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string Type { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Room Room { get; set; } = null!;
}
