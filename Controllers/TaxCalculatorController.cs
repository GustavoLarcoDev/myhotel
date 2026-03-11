using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyHotel.Web.Controllers;

[Authorize]
public class TaxCalculatorController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
