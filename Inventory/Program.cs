using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Inventory.Models;

namespace Inventory;

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

        //***** Review *****
        builder.Services.AddDbContext<Review_DbContext>(opts =>
        {
            opts.UseSqlServer(builder.Configuration["ConnectionStrings:ReviewDbConnection"],
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        //***** Inventory *****
        builder.Services.AddDbContext<Inventory_DbContext>(opts =>
        {
            opts.UseSqlServer(builder.Configuration["ConnectionStrings:InventoryDbConnection"]);
        });


        //****************************** Services *****************************
        builder.Services.AddControllersWithViews();
        builder.Services.AddTransient<IEmailSender, EmailSender>();
        builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, opts =>
        {
            opts.LoginPath = "/Identity/Login";
            opts.AccessDeniedPath = "/Identity/AccessDenied";
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
        identity_DbContext.Database.Migrate();
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

        }
        if (await roleManager.FindByNameAsync("Identity_Admins") == null)
        {
            await roleManager.CreateAsync(new Identity_RoleModel("Identity_Admins") { Description = "Top admins of the Identity service." });
            await userManager.AddToRoleAsync(admin, "Identity_Admins");
        }
        if (await roleManager.FindByNameAsync("Review_Admins") == null)
        {
            await roleManager.CreateAsync(new Identity_RoleModel("Review_Admins") { Description = "Top admins of the Review service." });
            await userManager.AddToRoleAsync(admin, "Review_Admins");
        }
        if (await roleManager.FindByNameAsync("Inventory_Admins") == null)
        {
            await roleManager.CreateAsync(new Identity_RoleModel("Inventory_Admins") { Description = "Top admins of the Inventory service." });
            await userManager.AddToRoleAsync(admin, "Inventory_Admins");
        }

        //***** Seed Empty Review for test *****
        Review_DbContext reviewDb = app.Services.CreateScope().ServiceProvider.GetRequiredService<Review_DbContext>();
        reviewDb.Database.Migrate();
        if (!reviewDb.Reviews.Any())
        {
            Review_ReviewDbModel testReview = new()
            {
                Guid = "testReview"
            };
            await reviewDb.Reviews.AddAsync(testReview);
            await reviewDb.SaveChangesAsync();
        }


        app.Run();
    }
}
