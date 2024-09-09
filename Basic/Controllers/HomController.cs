using Microsoft.AspNetCore.Mvc;

namespace Basic.Controllers;

public class HomeController : Controller
{
    /*readonly IWebHostEnvironment env;

    public HomeController(IWebHostEnvironment _env)
    {
        env = _env;
    }*/

    public IActionResult Index()
    {
        return View();
    }
}