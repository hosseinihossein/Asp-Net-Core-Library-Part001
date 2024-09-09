using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicket.Models;

namespace SupportTicket.Controllers;

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