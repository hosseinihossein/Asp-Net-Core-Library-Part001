using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventory.Models;
using System.Text.Json;
using System.IO.Compression;
using System.Text;

namespace Inventory.Controllers;

[AutoValidateAntiforgeryToken]
public class InventoryController : Controller
{
    readonly UserManager<Identity_UserModel> userManager;
    readonly IWebHostEnvironment env;
    readonly char ds = Path.DirectorySeparatorChar;
    readonly Inventory_DbContext inventoryDb;
    readonly Review_DbContext reviewDb;

    public InventoryController(UserManager<Identity_UserModel> userManager, IWebHostEnvironment env,
    Inventory_DbContext inventoryDb, Review_DbContext reviewDb)
    {
        this.userManager = userManager;
        this.env = env;
        this.inventoryDb = inventoryDb;
        this.reviewDb = reviewDb;
    }

    public async Task<IActionResult> Index(string? category, int page = 1)
    {
        List<Inventory_ItemDbModel> itemsDbModel;
        if (category == null)
        {
            itemsDbModel = await inventoryDb.Items.OrderByDescending(item => item.Date).ToListAsync();
        }
        else
        {
            itemsDbModel = await inventoryDb.Items.Where(item => item.Category == category)
            .OrderByDescending(item => item.Date)
            .ToListAsync();
        }
        //itemsDbModel.Reverse();
        itemsDbModel = itemsDbModel.Skip((page - 1) * 18).Take(18).ToList();

        List<Inventory_ItemModel> itemModels = new();
        foreach (Inventory_ItemDbModel itemDbModel in itemsDbModel)
        {
            Inventory_ItemModel itemModel = new()
            {
                Guid = itemDbModel.Guid,
                Name = itemDbModel.Name,
                Category = itemDbModel.Category,
                Description = itemDbModel.Description,
                Owner = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == itemDbModel.OwnerGuid),
                ReviewModel = await ReviewProcess.GetReviewModel(itemDbModel.ReviewGuid, reviewDb, userManager) ?? new()
            };
            itemModels.Add(itemModel);
        }

        ViewBag.CurrentPage = page;
        ViewBag.LastPAge = Math.Ceiling(itemModels.Count / 18.0);
        return View(itemModels);
    }

    public async Task<IActionResult> Item(string itemGuid)
    {
        Inventory_ItemDbModel? itemDbModel = await inventoryDb.Items.FirstOrDefaultAsync(item => item.Guid == itemGuid);
        if (itemDbModel == null)
        {
            object o = "Couldn't fing item!";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        Inventory_ItemModel itemModel = new()
        {
            Guid = itemDbModel.Guid,
            Name = itemDbModel.Name,
            Category = itemDbModel.Category,
            Description = itemDbModel.Description,
            Owner = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == itemDbModel.OwnerGuid),
            ReviewModel = await ReviewProcess.GetReviewModel(itemDbModel.ReviewGuid, reviewDb, userManager) ?? new()
        };

        return View(itemModel);
    }

    [Authorize]
    public IActionResult AddItem()
    {
        return View(new Inventory_FormModel());
    }

    [Authorize(Roles = "Inventory_Admins,Inventory_Item_Creators")]
    [HttpPost]
    public async Task<IActionResult> SubmitNewItem(Inventory_FormModel formModel)
    {
        if (ModelState.IsValid)
        {
            Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
            if (user is null)
            {
                return View();
            }

            Review_ReviewDbModel review = new()
            {
                Guid = Guid.NewGuid().ToString().Replace("-", "")
            };
            await reviewDb.Reviews.AddAsync(review);
            await reviewDb.SaveChangesAsync();

            Inventory_ItemDbModel itemDbModel = new()
            {
                Guid = Guid.NewGuid().ToString().Replace("-", ""),
                Name = formModel.Name,
                Category = formModel.Category,
                Description = formModel.Description,
                OwnerGuid = user.UserGuid,
                ReviewGuid = review.Guid
            };

            await inventoryDb.Items.AddAsync(itemDbModel);
            await inventoryDb.SaveChangesAsync();

            if (formModel.ImageFile != null)
            {
                string imagePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}{itemDbModel.Guid}";
                using (FileStream fs = System.IO.File.Create(imagePath))
                {
                    await formModel.ImageFile.CopyToAsync(fs);
                }
            }

            return RedirectToAction(nameof(Index), new { category = itemDbModel.Category });
        }
        return View(nameof(AddItem), new Inventory_FormModel());
    }

    [Authorize(Roles = "Inventory_Admins,Inventory_Item_Creators")]
    public async Task<IActionResult> DeleteItem(string itemGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            object o1 = "Couldn't find User!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        Inventory_ItemDbModel? itemDbModel = await inventoryDb.Items.FirstOrDefaultAsync(item => item.Guid == itemGuid);
        if (itemDbModel == null)
        {
            object o1 = "Couldn't find Item!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        if (await userManager.IsInRoleAsync(user, "Inventory_Admins") ||
        user.UserGuid == itemDbModel.OwnerGuid)
        {
            Review_ReviewDbModel? review = await reviewDb.Reviews.FirstOrDefaultAsync(r => r.Guid == itemDbModel.ReviewGuid);
            if (review is not null)
            {
                reviewDb.Reviews.Remove(review);
                await reviewDb.SaveChangesAsync();

            }
            inventoryDb.Items.Remove(itemDbModel);
            await inventoryDb.SaveChangesAsync();

            string imagePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}{itemDbModel.Guid}";
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
            return RedirectToAction("Index", "Inventory");
        }

        object o = "Only inventory_admins and the item owner can Delete the Item!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    public IActionResult ItemImage(string itemGuid)
    {
        string imagePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}{itemGuid}";
        if (System.IO.File.Exists(imagePath))
        {
            return PhysicalFile(imagePath, "Image/*");
        }
        else
        {
            imagePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}defaultItemImage.webp";
            return PhysicalFile(imagePath, "Image/*");
        }
    }

    //***************************** Backup ****************************
    [Authorize(Roles = "Inventory_Admins")]
    public IActionResult Backup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
        string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
        if (System.IO.File.Exists(backupZipFilePath))
        {
            ViewBag.BackupDate = System.IO.File.GetCreationTime(backupZipFilePath);
        }

        ViewBag.ControllerName = "Inventory";
        ViewBag.BackupDescription = "You also need to backup Reviews";
        return View();
    }

    [Authorize(Roles = "Inventory_Admins")]
    public async Task<IActionResult> RenewBackup()
    {
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
        if (!Directory.Exists(mainDirectoryPath))
        {
            Directory.CreateDirectory(mainDirectoryPath);
        }

        string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        if (Directory.Exists(dataDirectoryPath))
        {
            Directory.Delete(dataDirectoryPath, true);
        }
        Directory.CreateDirectory(dataDirectoryPath);

        string backupZipFilePath = $"{mainDirectoryPath}{ds}backup.zip";
        if (System.IO.File.Exists(backupZipFilePath))
        {
            System.IO.File.Delete(backupZipFilePath);
        }

        foreach (Inventory_ItemDbModel itemDbModel in await inventoryDb.Items.ToListAsync())
        {
            Inventory_BackupModel model = new()
            {
                Guid = itemDbModel.Guid,
                Name = itemDbModel.Name,
                Category = itemDbModel.Category,
                Description = itemDbModel.Description,
                OwnerGuid = itemDbModel.OwnerGuid,
                ReviewGuid = itemDbModel.ReviewGuid,
                Date = itemDbModel.Date,
            };
            string json = JsonSerializer.Serialize(model);

            string itemDirectoryPath = $"{dataDirectoryPath}{ds}{itemDbModel.Guid}";
            Directory.CreateDirectory(itemDirectoryPath);

            string jsonDataFilePath = $"{itemDirectoryPath}{ds}data.json";
            await System.IO.File.WriteAllTextAsync(jsonDataFilePath, json);

            string itemImageSourceFilePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}{itemDbModel.Guid}";
            if (System.IO.File.Exists(itemImageSourceFilePath))
            {
                string itemImageDestinationFilePath = $"{itemDirectoryPath}{ds}image";
                using (FileStream fsSource = System.IO.File.Open(itemImageSourceFilePath, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream fsDestination = System.IO.File.Create(itemImageDestinationFilePath))
                    {
                        await fsSource.CopyToAsync(fsDestination);
                    }
                }
            }
        }

        ZipFile.CreateFromDirectory(dataDirectoryPath, backupZipFilePath);

        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = "Inventory_Admins")]
    public IActionResult DownloadBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
        if (Directory.Exists(backupDirectory))
        {
            string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                return PhysicalFile(backupZipFilePath, "Application/zip", "InventoryBackup.zip");
            }
        }
        object o = "backup file Not found!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize(Roles = "Inventory_Admins")]
    public IActionResult DeleteBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
        if (Directory.Exists(backupDirectory))
        {
            string backupDataDirectoryPath = $"{backupDirectory}{ds}Data";
            if (Directory.Exists(backupDataDirectoryPath))
            {
                Directory.Delete(backupDataDirectoryPath, true);
            }

            string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                System.IO.File.Delete(backupZipFilePath);
            }
        }

        return RedirectToAction(nameof(Backup));
    }

    [HttpPost]
    [Authorize(Roles = "Inventory_Admins")]
    public async Task<IActionResult> UploadBackup(IFormFile backupZipFile)
    {
        if (ModelState.IsValid)
        {
            string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
            if (!Directory.Exists(mainDirectoryPath))
            {
                Directory.CreateDirectory(mainDirectoryPath);
            }

            string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
            if (Directory.Exists(dataDirectoryPath))
            {
                Directory.Delete(dataDirectoryPath, true);
            }
            Directory.CreateDirectory(dataDirectoryPath);

            string backupZipFilePath = $"{mainDirectoryPath}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                System.IO.File.Delete(backupZipFilePath);
            }

            using (FileStream fs = System.IO.File.Create(backupZipFilePath))
            {
                await backupZipFile.CopyToAsync(fs);
            }

            ZipFile.ExtractToDirectory(backupZipFilePath, dataDirectoryPath);
        }
        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = "Inventory_Admins")]
    public async Task<IActionResult> RecoverLastBackup()
    {
        List<Inventory_ItemDbModel> itemDbModels = new();
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}Inventory{ds}Backup";
        string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        DirectoryInfo dataDirectoryInfo = new DirectoryInfo(dataDirectoryPath);
        foreach (DirectoryInfo itemDirInfo in dataDirectoryInfo.EnumerateDirectories())
        {
            string jsonDataFilePath = $"{itemDirInfo.FullName}{ds}data.json";
            string json = await System.IO.File.ReadAllTextAsync(jsonDataFilePath, Encoding.UTF8);
            Inventory_BackupModel? model =
            JsonSerializer.Deserialize<Inventory_BackupModel>(json);
            if (model is null)
            {
                continue;
            }
            Inventory_ItemDbModel? itemDbModel =
            await inventoryDb.Items.FirstOrDefaultAsync(item => item.Guid == model.Guid);
            if (itemDbModel == null)
            {
                itemDbModel = new()
                {
                    Guid = model.Guid,
                    Name = model.Name,
                    Category = model.Category,
                    Description = model.Description,
                    OwnerGuid = model.OwnerGuid,
                    ReviewGuid = model.ReviewGuid,
                    Date = model.Date,
                };
                itemDbModels.Add(itemDbModel);

                string itemImageDestinationFilePath = $"{itemDirInfo.FullName}{ds}image";
                if (System.IO.File.Exists(itemImageDestinationFilePath))
                {
                    string itemImageSourceFilePath = $"{env.WebRootPath}{ds}Images{ds}Items{ds}{itemDbModel.Guid}";
                    using (FileStream fsDestination = System.IO.File.Open(itemImageDestinationFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (FileStream fsSource = System.IO.File.Create(itemImageSourceFilePath))
                        {
                            await fsDestination.CopyToAsync(fsSource);
                        }
                    }
                }
            }
        }
        await inventoryDb.Items.AddRangeAsync(itemDbModels);
        await inventoryDb.SaveChangesAsync();

        ViewBag.ResultState = "success";
        object o = $"Recovery for Inventory has done successfully.";
        return View("Result", o);
    }


}