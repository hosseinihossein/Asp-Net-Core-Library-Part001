using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SupportTicket.Models;
public class SupportTicket_DbContext : DbContext
{
    public SupportTicket_DbContext(DbContextOptions<SupportTicket_DbContext> options) : base(options) { }

    public DbSet<SupportTicket_DbModel> Messages { get; set; } = null!;
}

public class SupportTicket_DbModel
{
    public int Id { get; set; }
    public string MessageGuid { get; set; } = string.Empty;
    public string FromUserGuid { get; set; } = string.Empty;
    public List<string>? ToUsersGuids { get; set; }
    public bool ToAll { get; set; } = false;
    public string Subject { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; } = null;
    public List<string> SeenByClientsGuids { get; set; } = new();
}

public class SupportTicket_FormModel
{
    public string Subject { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public IFormFile? AttachedFile { get; set; } = null;
    public List<Identity_UserAndRolesModel>? UsersList { get; set; }
    public string ToUsernames { get; set; } = string.Empty;
}

/*public class SupportTickets_UsersListModel(Identity_UserModel user, List<string> roles)
{
    public Identity_UserModel Identity_UserModel { get; set; } = user;
    public List<string> Roles { get; set; } = roles;

}*/

public class SupportTicket_ListRowModel
{
    public string FromUsername { get; set; } = string.Empty;
    public int FromProfileImageVersion { get; set; } = 0;
    public string Subject { get; set; } = string.Empty;
    public string MessageGuid { get; set; } = string.Empty;
    public bool IsSeen { get; set; } = false;
}

public class SupportTicket_OpenModel
{
    public string MessageGuid { get; set; } = string.Empty;
    public Identity_UserModel? FromUser { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; } = null;
    public List<Identity_UserAndRolesModel> SeenByClients { get; set; } = new();
}

public class SupportTicket_BackupModel
{
    public string MessageGuid { get; set; } = string.Empty;
    public string FromUserGuid { get; set; } = string.Empty;
    public List<string> ToUsersGuids { get; set; } = new();
    public bool ToAll { get; set; } = false;
    public string Subject { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; } = string.Empty;
    public List<string> SeenByClientsGuids { get; set; } = new();
}

//************************* process *************************
public class SupportTicket_Process
{
    public static async Task<List<SupportTicket_ListRowModel>> GetMessagesList(UserManager<Identity_UserModel> userManager,
    Identity_UserModel user, SupportTicket_DbContext messageDb)
    {
        List<SupportTicket_ListRowModel> messageList = new();

        if (await userManager.IsInRoleAsync(user, "SupportTicket_Admins"))
        {
            List<SupportTicket_DbModel> allMessages = await messageDb.Messages.ToListAsync();
            allMessages.Reverse();
            foreach (SupportTicket_DbModel messageDbModel in allMessages)
            {
                Identity_UserModel? fromUser = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == messageDbModel.FromUserGuid);
                SupportTicket_ListRowModel rowData = new()
                {
                    FromUsername = fromUser?.UserName ?? "_",
                    FromProfileImageVersion = fromUser?.ProfileImageVersion ?? 0,
                    Subject = messageDbModel.Subject,
                    MessageGuid = messageDbModel.MessageGuid,
                    IsSeen = messageDbModel.SeenByClientsGuids.Contains(user.UserGuid),
                };
                messageList.Add(rowData);
            }
        }
        else
        {
            List<SupportTicket_DbModel> allMessages = await messageDb.Messages
            .Where(m => m.FromUserGuid == user.UserGuid ||
            m.ToAll ||
            (m.ToUsersGuids != null && m.ToUsersGuids.Contains(user.UserGuid)))
            .ToListAsync();
            allMessages.Reverse();
            foreach (SupportTicket_DbModel messageDbModel in allMessages)
            {
                Identity_UserModel? fromUser = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == messageDbModel.FromUserGuid);
                SupportTicket_ListRowModel rowData = new()
                {
                    FromUsername = fromUser?.UserName ?? "_",
                    FromProfileImageVersion = fromUser?.ProfileImageVersion ?? 0,
                    Subject = messageDbModel.Subject,
                    MessageGuid = messageDbModel.MessageGuid,
                    IsSeen = messageDbModel.SeenByClientsGuids.Contains(user.UserGuid)
                };
                messageList.Add(rowData);
            }
        }

        return messageList;
    }
}