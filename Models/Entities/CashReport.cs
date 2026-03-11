namespace MyHotel.Web.Models.Entities;

public class CashReport
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Date { get; set; } = "";
    public string Shift { get; set; } = ""; // AM, PM, Night

    // Bill counts
    public int Bills100 { get; set; }
    public int Bills50 { get; set; }
    public int Bills20 { get; set; }
    public int Bills10 { get; set; }
    public int Bills5 { get; set; }
    public int Bills1 { get; set; }

    // Coin counts
    public int Coins25 { get; set; } // Quarters
    public int Coins10 { get; set; } // Dimes
    public int Coins5 { get; set; }  // Nickels
    public int Coins1 { get; set; }  // Pennies

    // Coin roll counts (sealed rolls)
    public int Rolls25 { get; set; } // Quarter rolls ($10 each)
    public int Rolls10 { get; set; } // Dime rolls ($5 each)
    public int Rolls5 { get; set; }  // Nickel rolls ($2 each)
    public int Rolls1 { get; set; }  // Penny rolls ($0.50 each)

    // Calculated total from bill/coin/roll count
    public decimal CashTotal { get; set; }

    // Legacy/extra fields
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
