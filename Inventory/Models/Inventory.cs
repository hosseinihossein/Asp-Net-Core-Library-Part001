using Microsoft.EntityFrameworkCore;

namespace Inventory.Models;

public class Inventory_ItemDbModel
{
    public int Id { get; set; }
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerGuid { get; set; } = string.Empty;
    public string ReviewGuid { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}

public class Inventory_DbContext : DbContext
{
    public Inventory_DbContext(DbContextOptions<Inventory_DbContext> options) : base(options) { }

    public DbSet<Inventory_ItemDbModel> Items { get; set; } = null!;
}

public class Inventory_ItemModel
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Identity_UserModel? Owner { get; set; }
    public Review_ReviewModel ReviewModel { get; set; } = null!;
}

public class Inventory_FormModel
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IFormFile? ImageFile { get; set; }
}

//************************ Backup ***********************
public class Inventory_BackupModel
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerGuid { get; set; } = string.Empty;
    public string ReviewGuid { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}