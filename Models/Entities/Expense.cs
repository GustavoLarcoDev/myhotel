namespace MyHotel.Web.Models.Entities;

public class Expense
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public int? VendorId { get; set; }
    public DateTime Date { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public Vendor? Vendor { get; set; }
}

public class BudgetItem
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Category { get; set; } = "";
    public decimal PlannedAmount { get; set; }
    public string Period { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
