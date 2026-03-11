namespace MyHotel.Web.Models.Entities;

public class InventoryCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "general"; // market, cleaning, maintenance
    public int HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;
    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

public class InventoryItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int HotelId { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public int MinStock { get; set; }
    public string? Location { get; set; }
    public decimal? Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public InventoryCategory Category { get; set; } = null!;
    public Hotel Hotel { get; set; } = null!;
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public string Type { get; set; } = "in"; // in, out
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public InventoryItem Item { get; set; } = null!;
}
