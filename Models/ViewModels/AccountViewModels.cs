using System.ComponentModel.DataAnnotations;
using MyHotel.Web.Models.Entities;

namespace MyHotel.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Email or Phone")]
    public string EmailOrPhone { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}

public class AdminCreateUserViewModel
{
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    [Required] [EmailAddress] public string Email { get; set; } = "";
    public string? Phone { get; set; }
    [Required] [MinLength(4)] public string Password { get; set; } = "";
    public int HotelId { get; set; }
    public AppRole Role { get; set; }
    public int? DepartmentId { get; set; }
}

public class AdminCreateHotelViewModel
{
    [Required] public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Phone { get; set; }
    public string? GmEmail { get; set; }
    public string? GmFirstName { get; set; }
    public string? GmLastName { get; set; }
    public string? GmPassword { get; set; }
}

public class AdminCreateGMViewModel
{
    // GM info
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    [Required] [EmailAddress] public string Email { get; set; } = "";
    public string? Phone { get; set; }
    [Required] [MinLength(4)] public string Password { get; set; } = "";

    // First hotel info
    [Required] [Display(Name = "Hotel Name")] public string HotelName { get; set; } = "";
    [Display(Name = "Address")] public string? HotelAddress { get; set; }
    [Display(Name = "City")] public string? HotelCity { get; set; }
    [Display(Name = "State")] public string? HotelState { get; set; }
    [Display(Name = "Phone")] public string? HotelPhone { get; set; }
}
