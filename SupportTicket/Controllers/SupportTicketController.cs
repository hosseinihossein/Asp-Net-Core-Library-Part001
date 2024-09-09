using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportTicket.Models;
using System.Net;
using System.Text.Json;
using System.IO.Compression;
using System.Text;

namespace SupportTicket.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class SupportTicketController : Controller
{
    readonly UserManager<Identity_UserModel> userManager;
    readonly IWebHostEnvironment env;
    readonly char ds = Path.DirectorySeparatorChar;
    readonly SupportTicket_DbContext messageDb;

    public SupportTicketController(UserManager<Identity_UserModel> userManager, IWebHostEnvironment env,
    SupportTicket_DbContext messageDb)
    {
        this.userManager = userManager;
        this.env = env;
        this.messageDb = messageDb;
    }

    public async Task<IActionResult> Index()
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return NotFound();
        }

        /***** Notification *****
        await Notification_Process.RemoveNotif_FromUser("msg", userManager, user);*/

        //************************
        return View(await SupportTicket_Process.GetMessagesList(userManager, user, messageDb));
    }

    public async Task<IActionResult> NewMessage(string toUsernames = "")
    {
        List<Identity_UserAndRolesModel> userListModel_List = [];
        foreach (Identity_UserModel user in await userManager.Users.ToListAsync())
        {
            if (user is null || user.UserName == "admin") continue;
            Identity_UserAndRolesModel userListModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            userListModel_List.Add(userListModel);
        }
        ViewBag.ShowCheckmarks = true;
        ViewBag.CheckedUsernames = toUsernames;
        return View(new SupportTicket_FormModel() { UsersList = userListModel_List, ToUsernames = toUsernames });
    }

    [HttpPost]
    public async Task<IActionResult> SubmitNewMessage(SupportTicket_FormModel messageFormModel)
    {
        if (ModelState.IsValid)
        {
            Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
            if (user == null)
            {
                return NotFound();
            }

            SupportTicket_DbModel messageDbModel;
            string messageGuid = Guid.NewGuid().ToString().Replace("-", "");

            if (await userManager.IsInRoleAsync(user, "SupportTicket_Admins"))
            {
                string[] toUsernames = messageFormModel.ToUsernames.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (toUsernames.Length == 0)
                {
                    ViewBag.ResultState = "danger";
                    object o1 = "Atleast one receiver needs to be selected!";
                    return RedirectToAction("Index", "Result", o1);
                }
                bool toAll = false;
                List<string>? toUserGuids = null;
                if (toUsernames.Length == 1 && toUsernames[0] == "all")
                {
                    toAll = true;
                }
                else
                {
                    toUserGuids = new();
                    foreach (string username in toUsernames)
                    {
                        Identity_UserModel? toUser = await userManager.FindByNameAsync(username);
                        if (toUser is not null)
                        {
                            toUserGuids.Add(toUser.UserGuid);
                        }
                    }
                }

                messageDbModel = new()
                {
                    MessageGuid = messageGuid,
                    FromUserGuid = user.UserGuid,
                    ToAll = toAll,
                    ToUsersGuids = toUserGuids,
                    Subject = messageFormModel.Subject,
                    MessageText = messageFormModel.MessageText,
                    AttachedFileName = messageFormModel.AttachedFile?.FileName
                };
            }
            else
            {
                messageDbModel = new()
                {
                    MessageGuid = messageGuid,
                    FromUserGuid = user.UserGuid,
                    Subject = messageFormModel.Subject,
                    MessageText = messageFormModel.MessageText,
                    AttachedFileName = WebUtility.HtmlEncode(messageFormModel.AttachedFile?.FileName)
                };
            }

            await messageDb.Messages.AddAsync(messageDbModel);
            await messageDb.SaveChangesAsync();

            //***** save attached file *****
            if (messageFormModel.AttachedFile is not null)
            {
                string storageDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket";
                DirectoryInfo directoryInfo = Directory.CreateDirectory(storageDirectory + ds + messageGuid);
                string filePath = directoryInfo.FullName + ds + messageDbModel.AttachedFileName;
                using (FileStream fs = System.IO.File.Create(filePath))
                {
                    await messageFormModel.AttachedFile.CopyToAsync(fs);
                }
            }

            /***** Notifications *****
            if (messageDbModel.ToAll)
            {
                await Notification_Process.SetNewNotif_ToAll($"msg", userManager);
            }
            else if (messageDbModel.ToUsersGuids is not null)
            {
                foreach (string userGuid in messageDbModel.ToUsersGuids)
                {
                    await Notification_Process.SetNewNotif_ToUser("msg", userManager, userGuid);
                }
                foreach (Identity_UserModel adminUser in await userManager.GetUsersInRoleAsync("SupportTicket_Admins"))
                {
                    await Notification_Process.SetNewNotif_ToUser("msg", userManager, adminUser);
                }
            }
            else
            {
                foreach (Identity_UserModel adminUser in await userManager.GetUsersInRoleAsync("SupportTicket_Admins"))
                {
                    await Notification_Process.SetNewNotif_ToUser("msg", userManager, adminUser);
                }
            }*/

            //******************** success *********************
            ViewBag.ResultState = "success";
            object o = "Your message successfully sent!";
            return View("Result", o);
        }

        List<Identity_UserAndRolesModel> userListModel_List = [];
        foreach (Identity_UserModel user in await userManager.Users.ToListAsync())
        {
            if (user is null || user.UserName == "admin") continue;
            Identity_UserAndRolesModel userListModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            userListModel_List.Add(userListModel);
        }
        ViewBag.ShowCheckmarks = true;
        return View(nameof(NewMessage), new SupportTicket_FormModel() { UsersList = userListModel_List });
    }

    public async Task<IActionResult> Open(string messageGuid)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        SupportTicket_DbModel? messageDbModel = await messageDb.Messages.FirstOrDefaultAsync(m => m.MessageGuid == messageGuid);
        if (messageDbModel == null)
        {
            ViewBag.ResultState = "danger";
            object o1 = "Message Not Found!";
            return View("Result", o1);
        }

        if (await userManager.IsInRoleAsync(user, "SupportTicket_Admins") ||
            messageDbModel.ToAll ||
            (messageDbModel.ToUsersGuids != null && messageDbModel.ToUsersGuids.Contains(user.UserGuid)) ||
            messageDbModel.FromUserGuid == user.UserGuid)
        {
            if (!messageDbModel.SeenByClientsGuids.Contains(user.UserGuid))
            {
                messageDbModel.SeenByClientsGuids.Add(user.UserGuid);
                await messageDb.SaveChangesAsync();
            }

            Identity_UserModel? fromUser = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == messageDbModel.FromUserGuid);
            List<Identity_UserAndRolesModel> seenByClients = new();
            foreach (string userGuid in messageDbModel.SeenByClientsGuids)
            {
                Identity_UserModel seenUser = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid) ?? new() { UserGuid = userGuid };
                Identity_UserAndRolesModel userAndRolesModel = new(seenUser, (await userManager.GetRolesAsync(seenUser)).ToList());
                seenByClients.Add(userAndRolesModel);
            }
            SupportTicket_OpenModel openModel = new()
            {
                MessageGuid = messageDbModel.MessageGuid,
                FromUser = fromUser,
                Subject = messageDbModel.Subject,
                MessageText = messageDbModel.MessageText,
                AttachedFileName = messageDbModel.AttachedFileName,
                SeenByClients = seenByClients
            };
            return View(openModel);
        }

        ViewBag.ResultStatus = "danger";
        object o = "You don't have permition to open this message!";
        return View("Result", o);
    }

    public async Task<IActionResult> DownloadAttachedFile(string messageGuid)
    {
        string storageDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket";
        SupportTicket_DbModel? messageDbModel = await messageDb.Messages.FirstOrDefaultAsync(m => m.MessageGuid == messageGuid);
        if (messageDbModel == null)
        {
            ViewBag.ResultState = "danger";
            object o1 = "Message Not Found!";
            return View("Result", o1);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (await userManager.IsInRoleAsync(user, "SupportTicket_Admins") ||
            messageDbModel.ToAll ||
            (messageDbModel.ToUsersGuids != null && messageDbModel.ToUsersGuids.Contains(user.UserGuid)) ||
            messageDbModel.FromUserGuid == user.UserGuid)
        {
            if (messageDbModel.AttachedFileName is not null && Directory.Exists(storageDirectory + ds + messageGuid))
            {
                string filePath = storageDirectory + $"{ds}{messageGuid}{ds}{messageDbModel.AttachedFileName}";
                return PhysicalFile(filePath, "application/*", messageDbModel.AttachedFileName);
            }
            ViewBag.ResultState = "danger";
            object o1 = "File Not Found!";
            return View("Result", o1);
        }

        ViewBag.ResultStatus = "danger";
        object o = "You don't have permition to open this message!";
        return View("Result", o);
    }

    [Authorize(Roles = "SupportTicket_Admins")]
    public async Task<IActionResult> DeleteMessage(string messageGuid)
    {
        if (User.Identity?.Name != "admin")
        {
            ViewBag.ResultState = "danger";
            object o = "Only \'admin\' can Delete messages!";
            return View("Result", o);
        }

        string storageDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket";

        SupportTicket_DbModel? messageDbModel = await messageDb.Messages.FirstOrDefaultAsync(m => m.MessageGuid == messageGuid);
        if (messageDbModel == null)
        {
            if (Directory.Exists(storageDirectory + ds + messageGuid))
            {
                Directory.Delete(storageDirectory + ds + messageGuid, true);
            }
            ViewBag.ResultState = "danger";
            object o = "Message Not Found!";
            return View("Result", o);
        }

        messageDb.Messages.Remove(messageDbModel);
        await messageDb.SaveChangesAsync();
        if (Directory.Exists(storageDirectory + ds + messageGuid))
        {
            Directory.Delete(storageDirectory + ds + messageGuid, true);
        }

        return RedirectToAction(nameof(Index));
    }

    //************************************ Backup ************************************
    [Authorize(Roles = "SupportTicket_Admins")]
    public IActionResult Backup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
        string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
        if (System.IO.File.Exists(backupZipFilePath))
        {
            ViewBag.BackupDate = System.IO.File.GetCreationTime(backupZipFilePath);
        }

        ViewBag.ControllerName = "SupportTicket";
        return View();
    }

    [Authorize(Roles = "SupportTicket_Admins")]
    public async Task<IActionResult> RenewBackup()
    {
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
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

        foreach (SupportTicket_DbModel messageDbModel in await messageDb.Messages.ToListAsync())
        {
            SupportTicket_BackupModel model = new()
            {
                MessageGuid = messageDbModel.MessageGuid,
                FromUserGuid = messageDbModel.FromUserGuid,
                ToUsersGuids = messageDbModel.ToUsersGuids ?? new(),
                ToAll = messageDbModel.ToAll,
                Subject = messageDbModel.Subject,
                MessageText = messageDbModel.MessageText,
                AttachedFileName = messageDbModel.AttachedFileName,
                SeenByClientsGuids = messageDbModel.SeenByClientsGuids
            };
            string json = JsonSerializer.Serialize(model);

            string messageDirectoryPath = $"{dataDirectoryPath}{ds}{messageDbModel.MessageGuid}";
            Directory.CreateDirectory(messageDirectoryPath);

            string jsonDataFilePath = $"{messageDirectoryPath}{ds}data.json";
            await System.IO.File.WriteAllTextAsync(jsonDataFilePath, json);

            if (messageDbModel.AttachedFileName is not null)
            {
                string messageSourceFilePath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}" +
                $"{messageDbModel.MessageGuid}{ds}{messageDbModel.AttachedFileName}";
                if (System.IO.File.Exists(messageSourceFilePath))
                {
                    string messageDestinationAttachedFilePath = $"{messageDirectoryPath}{ds}attachedFile";
                    using (FileStream fsSource = System.IO.File.Open(messageSourceFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (FileStream fsDestination = System.IO.File.Create(messageDestinationAttachedFilePath))
                        {
                            await fsSource.CopyToAsync(fsDestination);
                        }
                    }
                }
            }
        }

        ZipFile.CreateFromDirectory(dataDirectoryPath, backupZipFilePath);

        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = "SupportTicket_Admins")]
    public IActionResult DownloadBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
        if (Directory.Exists(backupDirectory))
        {
            string backupZipFilePath = $"{backupDirectory}{ds}backup.zip";
            if (System.IO.File.Exists(backupZipFilePath))
            {
                return PhysicalFile(backupZipFilePath, "Application/zip", "SupportTicketBackup.zip");
            }
        }
        object o = "backup file Not found!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize(Roles = "SupportTicket_Admins")]
    public IActionResult DeleteBackup()
    {
        string backupDirectory = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
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
    [Authorize(Roles = "SupportTicket_Admins")]
    public async Task<IActionResult> UploadBackup(IFormFile backupZipFile)
    {
        if (ModelState.IsValid)
        {
            string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
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

    [Authorize(Roles = "SupportTicket_Admins")]
    public async Task<IActionResult> RecoverLastBackup()
    {
        List<SupportTicket_DbModel> messageDbModels = new();
        string mainDirectoryPath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}Backup";
        string dataDirectoryPath = $"{mainDirectoryPath}{ds}Data";
        DirectoryInfo dataDirectoryInfo = new DirectoryInfo(dataDirectoryPath);
        foreach (DirectoryInfo messageDirInfo in dataDirectoryInfo.EnumerateDirectories())
        {
            string jsonDataFilePath = $"{messageDirInfo.FullName}{ds}data.json";
            string json = await System.IO.File.ReadAllTextAsync(jsonDataFilePath, Encoding.UTF8);
            SupportTicket_BackupModel? model =
            JsonSerializer.Deserialize<SupportTicket_BackupModel>(json);
            if (model is null)
            {
                continue;
            }
            SupportTicket_DbModel? messageDbModel =
            await messageDb.Messages.FirstOrDefaultAsync(m => m.MessageGuid == model.MessageGuid);
            if (messageDbModel is null)
            {
                messageDbModel = new()
                {
                    MessageGuid = model.MessageGuid,
                    FromUserGuid = model.FromUserGuid,
                    ToUsersGuids = model.ToUsersGuids,
                    ToAll = model.ToAll,
                    Subject = model.Subject,
                    MessageText = model.MessageText,
                    AttachedFileName = model.AttachedFileName,
                    SeenByClientsGuids = model.SeenByClientsGuids,
                };
                //await messageDb.Messages.AddAsync(messageDbModel);
                //await messageDb.SaveChangesAsync();
                messageDbModels.Add(messageDbModel);

                if (messageDbModel.AttachedFileName != null)
                {
                    string messageDestinationAttachedFilePath = $"{messageDirInfo.FullName}{ds}attachedFile";
                    if (System.IO.File.Exists(messageDestinationAttachedFilePath))
                    {
                        Directory.CreateDirectory($"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}{messageDbModel.MessageGuid}");
                        string messageSourceFilePath = $"{env.ContentRootPath}{ds}SpecificStorage{ds}SupportTicket{ds}" +
                        $"{messageDbModel.MessageGuid}{ds}{messageDbModel.AttachedFileName}";
                        using (FileStream fsDestination = System.IO.File.Open(messageDestinationAttachedFilePath, FileMode.Open, FileAccess.Read))
                        {
                            using (FileStream fsSource = System.IO.File.Create(messageSourceFilePath))
                            {
                                await fsDestination.CopyToAsync(fsSource);
                            }
                        }
                    }
                }
            }
        }
        await messageDb.Messages.AddRangeAsync(messageDbModels);
        await messageDb.SaveChangesAsync();

        ViewBag.ResultState = "success";
        object o = $"Recovery for SupportTicket has done successfully.";
        return View("Result", o);
    }

}