namespace MyHotel.Web.Models.Entities;

public class CashReport
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Date { get; set; } = "";
    public string Shift { get; set; } = ""; // AM, PM, Night
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal CreditCardTotal { get; set; }
    public decimal Variance { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
}
