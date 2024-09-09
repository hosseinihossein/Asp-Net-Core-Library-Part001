using Microsoft.AspNetCore.Mvc;

namespace UploadLargeFormFile.Components;

public class NavBarComponent : ViewComponent
{
    readonly char ds = Path.DirectorySeparatorChar;
    readonly IWebHostEnvironment env;

    public NavBarComponent(IWebHostEnvironment _env)
    {
        env = _env;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        return View();
    }
}