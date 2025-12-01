

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Superchef.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options)
    {

    }

    public DbSet<AccountType> AccountTypes { get; set; }
    public DbSet<Venue> Venues { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Verification> Verifications { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<Keyword> Keywords { get; set; }
    public DbSet<Variant> Variants { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<Favourite> Favourites { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Slot> Slots { get; set; }
    public DbSet<SlotTemplate> SlotTemplates { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}

#nullable disable warnings

public class AccountType
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Account> Accounts { get; set; } = [];
}

public class Venue
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Store> Stores { get; set; } = [];
}

public class Category
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }

    public List<Item> Items { get; set; } = [];
}

public class Account
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(12)]
    public string PhoneNumber { get; set; }
    [MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string PasswordHash { get; set; }
    [MaxLength(50)]
    public string? Image { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DeletionAt { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; } = false;
    [Precision(15, 2)]
    public int AccountTypeId { get; set; }
    [NotMapped]
    public string Status
    {
        get
        {
            if (IsBanned) return "Banned";

            if (DeletionAt != null) return "ToDelete";

            if (IsDeleted) return "Deleted";

            if (LockoutEnd != null) return "Timeout";

            return "Active";
        }
    }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AccountType AccountType { get; set; }
    public List<Device> Devices { get; set; } = [];
    public List<Verification> Verifications { get; set; } = [];
    public List<Cart> Carts { get; set; } = [];
    public List<Store> Stores { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<Favourite> Favourites { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<AuditLog> AuditLogs { get; set; } = [];
}

public class Device
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Address { get; set; }
    [MaxLength(20)]
    public string DeviceOS { get; set; }
    [MaxLength(20)]
    public string DeviceType { get; set; }
    [MaxLength(20)]
    public string DeviceBrowser { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsVerified { get; set; }
    public int AccountId { get; set; }

    public Account Account { get; set; }
    public Verification Verification { get; set; }
    public List<Session> Sessions { get; set; } = [];
}

public class Session
{
    [Key]
    [MaxLength(50)]
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int DeviceId { get; set; }

    public Device Device { get; set; }
}

public class Verification
{
    [Key]
    [MaxLength(50)]
    public string Token { get; set; }
    [MaxLength(6)]
    public string OTP { get; set; }
    public bool IsVerified { get; set; }
    [MaxLength(30)]
    public string Action { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? AccountId { get; set; }
    public int? DeviceId { get; set; }

    public Account Account { get; set; }
    public Device? Device { get; set; }
}

public class Store
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string Slug { get; set; }
    [MaxLength(1000)]
    public string Description { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }
    public string? Banner { get; set; }
    public int SlotMaxOrders { get; set; }
    public string? StripeAccountId { get; set; }
    public bool HasPublishedFirstSlots { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public int VenueId { get; set; }
    public int AccountId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Venue Venue { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    public List<Slot> Slots { get; set; } = [];
    public List<SlotTemplate> SlotTemplates { get; set; } = [];
    public List<Item> Items { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}

public class Item
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string Slug { get; set; }
    [MaxLength(1000)]
    public string Description { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int CategoryId { get; set; }
    public int StoreId { get; set; }
    [NotMapped]
    public string Status
    {
        get
        {
            if (IsDeleted) return "Deleted";

            if (IsActive == false) return "Inactive";

            return "Active";
        }
    }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Category Category { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Store Store { get; set; }
    public List<Keyword> Keywords { get; set; } = [];
    public List<Variant> Variants { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<Favourite> Favourites { get; set; } = [];
}

public class Keyword
{
    public int Id { get; set; }
    [MaxLength(30)]
    public string Word { get; set; }

    public List<Item> Items { get; set; } = [];
}

public class Variant
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }
    [Precision(5, 2)]
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int ItemId { get; set; }
    [NotMapped]
    public string Status
    {
        get
        {
            if (IsDeleted) return "Deleted";

            if (IsActive == false) return "Inactive";

            return "Active";
        }
    }

    public Item Item { get; set; }
    public List<Cart> Carts { get; set; } = [];
    public List<OrderItem> OrderItems { get; set; } = [];
}

[PrimaryKey("VariantId", "AccountId")]
public class Cart
{
    public int VariantId { get; set; }
    public int AccountId { get; set; }
    public int Quantity { get; set; }

    public Variant Variant { get; set; }
    public Account Account { get; set; }
}

[PrimaryKey("ItemId", "AccountId")]
public class Favourite
{
    public int ItemId { get; set; }
    public int AccountId { get; set; }

    public Item Item { get; set; }
    public Account Account { get; set; }
}

[PrimaryKey("AccountId", "ItemId")]
public class Review
{
    public int AccountId { get; set; }
    public int ItemId { get; set; }
    public int Rating { get; set; }
    [MaxLength(200)]
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Account Account { get; set; }
    public Item Item { get; set; }
}

public class Slot
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MaxOrders { get; set; }
    public int StoreId { get; set; }

    public Store Store { get; set; }
    public List<Order> Orders { get; set; } = [];
}

public class SlotTemplate
{
    public int Id { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DayOfWeek { get; set; }

    public List<Store> Stores { get; set; } = [];
}

public class Order
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(12)]
    public string PhoneNumber { get; set; }
    [MaxLength(20)]
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }
    public int SlotId { get; set; }
    public int AccountId { get; set; }
    public int StoreId { get; set; }

    public Slot Slot { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Store Store { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Payment Payment { get; set; }
    public List<OrderItem> OrderItems { get; set; } = [];
}

[PrimaryKey("OrderId", "VariantId")]
public class OrderItem
{
    [MaxLength(50)]
    public string OrderId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    [Precision(5, 2)]
    public decimal Price { get; set; }

    public Order Order { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Variant Variant { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [Precision(10, 2)]
    public decimal Amount { get; set; }
    public string StripePaymentIntentId { get; set; }
    public string TransferStatus { get; set; }
    public string TransferId { get; set; }
    public bool IsRefunded { get; set; } = false;
    [MaxLength(20)]
    public string PaymentMethod { get; set; }
    [MaxLength(100)]
    public string? Details { get; set; }
    public string OrderId { get; set; }

    public Order Order { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Action { get; set; }
    public string Entity { get; set; }
    public int EntityId { get; set; }
    public int AccountId { get; set; }
    [NotMapped]
    public string Details
    {
        get
        {
            // customer / vendor
            if (Action == "ban") return $"Banned {Entity} with ID {EntityId}";

            // customer / vendor
            if (Action == "unban") return $"Unbanned {Entity} with ID {EntityId}";

            // customer / vendor
            if (Action == "revoke") return $"Restored {Entity} with ID {EntityId}";

            // customer / vendor / admin
            if (Action == "timeout") return $"Removed timeout for {Entity} with ID {EntityId}";

            // customer / vendor / admin
            if (Action == "logout") return $"Logged out {Entity} with ID {EntityId} from all devices";

            // vendor / admin / category / venue
            if (Action == "create") return $"Created {Entity} with ID {EntityId}";

            // vendor / admin / category / venue
            if (Action == "delete") return $"Deleted {Entity} with ID {EntityId}";

            // category / venue
            if (Action == "update") return $"Updated {Entity} with ID {EntityId}";

            return "Unknown action";
        }
    }
    [NotMapped]
    public string Icon
    {
        get
        {
            if (Action == "ban") return "🚫";
            if (Action == "unban") return "🔓";
            if (Action == "revoke") return "♻️";
            if (Action == "timeout") return "⏳";
            if (Action == "logout") return "🚪";
            if (Action == "create") return "➕";
            if (Action == "delete") return "🗑️";
            if (Action == "update") return "✏️";
            return "💀";
        }
    }

    public Account Account { get; set; }
}