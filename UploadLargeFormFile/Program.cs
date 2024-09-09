namespace UploadLargeFormFile;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //****************************** Services *****************************
        builder.Services.AddControllersWithViews();


        //******************************** app ********************************
        var app = builder.Build();

        app.UseStaticFiles();

        app.MapControllers();
        app.MapDefaultControllerRoute();

        //*********************************************************************


        app.Run();
    }
}
