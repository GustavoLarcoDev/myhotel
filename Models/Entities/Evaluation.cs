namespace MyHotel.Web.Models.Entities;

public class Evaluation
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string EmployeeId { get; set; } = "";
    public string EvaluatorId { get; set; } = "";
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public string? Period { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ApplicationUser Employee { get; set; } = null!;
    public ApplicationUser Evaluator { get; set; } = null!;
}

public class HousekeepingRating
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string HousekeeperId { get; set; } = "";
    public string RatedById { get; set; } = "";
    public int Stars { get; set; }
    public string? Room { get; set; }
    public string? Comments { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Hotel Hotel { get; set; } = null!;
    public ApplicationUser Housekeeper { get; set; } = null!;
    public ApplicationUser RatedBy { get; set; } = null!;
}
