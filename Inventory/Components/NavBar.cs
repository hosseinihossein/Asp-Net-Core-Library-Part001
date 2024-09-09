using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventory.Models;

namespace Inventory.Components;

public class NavBarComponent : ViewComponent
{
    readonly UserManager<Identity_UserModel> userManager;
    readonly char ds = Path.DirectorySeparatorChar;
    readonly IWebHostEnvironment env;

    public NavBarComponent(UserManager<Identity_UserModel> _userManager, IWebHostEnvironment _env)
    {
        env = _env;
        userManager = _userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        NavBarModel navBarModel;
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            navBarModel = new();
        }
        else
        {
            Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity.Name!);
            navBarModel = new() { User = user };
        }

        return View(navBarModel);
    }
}