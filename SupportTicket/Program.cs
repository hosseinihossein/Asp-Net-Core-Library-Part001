using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportTicket.Models;

namespace SupportTicket;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //******************* SQL Server DataBase Services *******************
        //***** Identity *****
        builder.Services.AddDbContext<Identity_DbContext>(opts =>
        {
            opts.UseSqlServer(builder.Configuration["ConnectionStrings:IdentityConnection"]);
        });
        builder.Services.AddIdentity<Identity_UserModel, Identity_RoleModel>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Tokens.EmailConfirmationTokenProvider = "emailTokenProvider";
        })
        .AddTokenProvider<Identity_EmailTokenProvider>("emailTokenProvider")
        .AddEntityFrameworkStores<Identity_DbContext>();

        //***** SupportTicket *****
        builder.Services.AddDbContext<SupportTicket_DbContext>(opts =>
        {
            opts.UseSqlServer(builder.Configuration["ConnectionStrings:SupportTicketDbConnection"]);
        });


        //****************************** Services *****************************
        builder.Services.AddControllersWithViews();
        builder.Services.AddTransient<IEmailSender, EmailSender>();
        builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, opts =>
        {
            opts.AccessDeniedPath = "/Identity/AccessDenied";
            opts.LoginPath = "/Identity/Login";
        });


        //******************************** app ********************************
        var app = builder.Build();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapDefaultControllerRoute();


        //************************** Seed SQL Server DataBases ***************************
        IWebHostEnvironment env = app.Services.GetRequiredService<IWebHostEnvironment>();

        //***** Seed "admin" Identity *****
        Identity_DbContext identity_DbContext = app.Services.CreateScope().ServiceProvider.GetRequiredService<Identity_DbContext>();
        //identity_DbContext.Database.Migrate();
        UserManager<Identity_UserModel> userManager = app.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<Identity_UserModel>>();
        RoleManager<Identity_RoleModel> roleManager = app.Services.CreateScope().ServiceProvider.GetRequiredService<RoleManager<Identity_RoleModel>>();
        Identity_UserModel? admin = await userManager.FindByNameAsync("admin");
        if (admin == null)
        {
            admin = new Identity_UserModel
            {
                UserName = "admin",
                Email = "admin@MyCompany.com",
                EmailConfirmed = true,
                UserGuid = "admin",
                PasswordLiteral = "P@ssw0rd"
            };
            IdentityResult result = await userManager.CreateAsync(admin, admin.PasswordLiteral);
            if (!result.Succeeded)
            {
                throw new Exception("Couldn't create admin identity!");
            }
        }

        //************** Role = Identity_Admins **************
        if (await roleManager.FindByNameAsync("Identity_Admins") == null)
        {
            await roleManager.CreateAsync(new Identity_RoleModel("Identity_Admins") { Description = "Top admins of the Identity service." });
            await userManager.AddToRoleAsync(admin, "Identity_Admins");
        }

        //************** Role = SupportTicket_Admins **************
        if (await roleManager.FindByNameAsync("SupportTicket_Admins") == null)
        {
            await roleManager.CreateAsync(new Identity_RoleModel("SupportTicket_Admins") { Description = "Top admins of the SupportTicket service." });
            await userManager.AddToRoleAsync(admin, "SupportTicket_Admins");
        }



        app.Run();
    }
}
