using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace Superchef.Models;

#nullable disable warnings

public class LoginVM
{
    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }

    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}

public class RegisterVM
{
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
    [Required(ErrorMessage = "Please complete the reCAPTCHA verification.")]
    public string RepatchaToken { get; set; }
}

public class ForgotPasswordVM
{
    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class ChangeEmailVM
{
    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Compare("Email", ErrorMessage = "Emails do not match.")]
    [DisplayName("Confirm Email")]
    public string ConfirmEmail { get; set; }
}

public class AccountProfileVM
{
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "{0} must be in the format 01X-XXXXXXX")]
    [DisplayName("Phone Number")]
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool RemoveImage { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile? Image { get; set; }
}

public class ChangePasswordVM
{
    [StringLength(100, ErrorMessage = "{1} must not exceed {0} characters.")]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string CurrentPassword { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string NewPassword { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm New Password")]
    public string ConfirmPassword { get; set; }
}

public class SearchVM
{
    public string? Query { get; set; }
    public string? Type { get; set; }
    public List<int> Venues { get; set; } = [];
    public string? Price { get; set; }
    public string? Rating { get; set; }
    public string? Sort { get; set; }
    public List<int> Categories { get; set; } = [];
    public int Page { get; set; } = 1;
    public IPagedList<object> Results { get; set; }

    // Available Options
    public List<string> AvailableTypes { get; set; } = [];
    public List<Venue> AvailableVenues { get; set; } = [];
    public Dictionary<string, string> AvailablePrices { get; set; } = [];
    public Dictionary<string, string> AvailableRatings { get; set; } = [];
    public Dictionary<string, string> AvailableStoreSortBy { get; set; } = [];
    public Dictionary<string, string> AvailableItemSortBy { get; set; } = [];
    public List<Category> AvailableCategories { get; set; } = [];
}

public class ItemInfoVM
{
    public Item Item { get; set; }
    public int TotalReviews { get; set; }
    [Precision(2, 1)]
    public decimal AverageRating { get; set; }
    public int TotalSold { get; set; }
    public string FilterRating { get; set; } = "all";
    public List<Review> Reviews { get; set; } = [];
    public ReviewInputVM NewReview { get; set; } = new();
    public AddToCartVM AddToCart { get; set; } = new();
}

public class AddToCartVM
{
    public int? Variant { get; set; }
    public int? Quantity { get; set; }
}

public class ReviewInputVM
{
    public int Id { get; set; }
    [Range(1, 5, ErrorMessage = "{0} must be between {1} and {2}")]
    public int Rating { get; set; }
    [StringLength(100, ErrorMessage = "{0} must not exceed {1} characters.")]
    public string Comment { get; set; }
}

public class CartStoreVM
{
    public int Id { get; set; }
    public Store Store { get; set; }
    public List<Cart> CartItems { get; set; } = [];
    public List<int> SelectedItems { get; set; } = [];
}

public class HistoryVM
{
    public string? Option { get; set; }
    public Dictionary<string, string> Options { get; set; } = [];
    public List<Order> Results { get; set; } = [];
}

public class ManageCustomerVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Account> Results { get; set; }
}

public class ManageVendorVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Min Stores Count")]
    public int? MinStoresCount { get; set; }
    [DisplayName("Max Stores Count")]
    public int? MaxStoresCount { get; set; }
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Account> Results { get; set; }
}

public class AddVendorVM
{
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }
}

public class ManageAdminVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<Account> Results { get; set; }
}

public class AddAdminVM
{
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(255, ErrorMessage = "{1} must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }
}

public class ManageVenueVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Stores Count")]
    public int? MinStoresCount { get; set; }
    [DisplayName("Max Stores Count")]
    public int? MaxStoresCount { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<Venue> Results { get; set; }
}

public class AddVenueVM
{
    [Remote("CheckNameExists", "Venue", ErrorMessage = "{0} already exists")]
    [MaxLength(50)]
    public string Name { get; set; }
}

public class EditVenueVM
{
    public int Id { get; set; }
    [Remote("CheckNameExists", "Venue", AdditionalFields = "Id", ErrorMessage = "{0} already exists")]
    [MaxLength(50)]
    public string Name { get; set; }
}

public class ManageCategoryVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Items Count")]
    public int? MinItemsCount { get; set; }
    [DisplayName("Max Items Count")]
    public int? MaxItemsCount { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<Category> Results { get; set; }
}

public class AddCategoryVM
{
    [MaxLength(50)]
    public string Name { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile Image { get; set; }
}

public class EditCategoryVM
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile? Image { get; set; }
}

public class ManageStoreVM
{
    public int VendorId { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<int> Venues { get; set; } = [];
    [DisplayName("Min Items Count")]
    public int? MinItemsCount { get; set; }
    [DisplayName("Max Items Count")]
    public int? MaxItemsCount { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<Venue> AvailableVenues { get; set; } = [];
    public IPagedList<Store> Results { get; set; }
}

public class AddStoreVM
{
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    [Remote("IsNameUnique", "Store", AdditionalFields = "Venue", ErrorMessage = "{0} has been taken in this venue.")]
    public string Name { get; set; }
    [StringLength(50, MinimumLength = 3, ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Store", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(1000, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Description { get; set; }
    [DisplayName("Max Orders Per Slot")]
    [Range(0, int.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public int SlotMaxOrders { get; set; } = 20;
    [Remote("CheckVenue", "Store", ErrorMessage = "{0} is invalid.")]
    public int Venue { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile Image { get; set; }

    public List<SelectListItem> AvailableVenues { get; set; } = [];
}

public class EditStoreVM
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string? StripeAccountId { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    [Remote("IsNameUnique", "Store", AdditionalFields = "Venue,Id", ErrorMessage = "{0} has been taken in this venue.")]
    public string Name { get; set; }
    [StringLength(50, MinimumLength = 3, ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Store", AdditionalFields = "Id", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(1000, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Description { get; set; }
    [DisplayName("Max Orders Per Slot")]
    [Range(0, int.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public int SlotMaxOrders { get; set; } = 20;
    [Remote("CheckVenue", "Store", ErrorMessage = "{0} is invalid.")]
    public int Venue { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile? Image { get; set; }
    public bool BannerRemoveImage { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double BannerImageScale { get; set; } = 1;
    public double BannerImageX { get; set; } = 0;
    public double BannerImageY { get; set; } = 0;
    [DisplayName("Banner Image")]
    public IFormFile? BannerImage { get; set; }

    public List<SelectListItem> AvailableVenues { get; set; } = [];
}

public class ManageSlotVM
{
    public int StoreId { get; set; }
    public string? Type { get; set; }
    public DateOnly? Date { get; set; }
    public int? Day { get; set; }
    public List<TimeOnly> Slots { get; set; } = [];
    public TimeOnly? AddSlot { get; set; }
    public TimeOnly? RemoveSlot { get; set; }
    public bool IsEnabledAll { get; set; }
    public bool IsDisabledAll { get; set; }
    public bool IsUseRecurring { get; set; }

    public List<string> AvailableTypes { get; set; } = [];
    public List<DateOnly> AvailableDates { get; set; } = [];
    public Dictionary<int, string> AvailableDays { get; set; } = [];
    public List<TimeOnly> AvailableSlots { get; set; } = [];
}

public class ManageItemVM
{
    public int? Id { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public string? Status { get; set; }
    public List<int> Categories { get; set; } = [];
    [DisplayName("Min Variants Count")]
    public int? MinVariantsCount { get; set; }
    [DisplayName("Max Variants Count")]
    public int? MaxVariantsCount { get; set; }
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public List<Category> AvailableCategories { get; set; } = [];
    public IPagedList<Item> Results { get; set; }
}

public class AddItemVM
{
    public int Id { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [StringLength(50, MinimumLength = 3, ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Item", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(1000, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Description { get; set; }
    public List<string> Keywords { get; set; } = [];
    [Remote("CheckCategory", "Item", ErrorMessage = "{0} is not a valid category.")]
    public int Category { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile Image { get; set; }

    public List<SelectListItem> AvailableCategories { get; set; } = [];
}

public class EditItemVM
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [StringLength(50, MinimumLength = 3, ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Item", AdditionalFields = "Id", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(1000, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Description { get; set; }
    public List<string> Keywords { get; set; } = [];
    [Remote("CheckCategory", "Item", ErrorMessage = "{0} is not a valid category.")]
    public int Category { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile? Image { get; set; }
    public bool Active { get; set; }

    public List<SelectListItem> AvailableCategories { get; set; } = [];
}

public class ManageVariantVM
{
    public int ItemId { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Min Price")]
    public decimal? MinPrice { get; set; }
    [DisplayName("Max Price")]
    public decimal? MaxPrice { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Variant> Results { get; set; }
}

public class AddVariantVM
{
    public int ItemId { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [Range(2.00, 200.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places.")]
    public decimal Price { get; set; }
    [DisplayName("Stock Count")]
    [Range(0, int.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public int StockCount { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile Image { get; set; }
}

public class EditVariantVM
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [Range(2.00, 200.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places.")]
    public decimal Price { get; set; }
    [DisplayName("Stock Count")]
    [Range(0, int.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public int StockCount { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; } = 1;
    public double ImageX { get; set; } = 0;
    public double ImageY { get; set; } = 0;
    public IFormFile? Image { get; set; }
    public bool Active { get; set; }
}

public class ManageOrderVM
{
    public int StoreId { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Pickup At From")]
    public DateTime? PickupFrom { get; set; }
    [DisplayName("Pickup At To")]
    public DateTime? PickupTo { get; set; }
    [DisplayName("Created At From")]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Created At To")]
    public DateTime? CreationTo { get; set; }

    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Order> Results { get; set; }
}

public class OrderCustomerVM
{
    public string Id { get; set; }
    [StringLength(50, ErrorMessage = "{1} must not exceed {0} characters.")]
    public string Name { get; set; }
    [StringLength(12, MinimumLength = 11, ErrorMessage = "{0} must be between {2} and {1} characters.")]
    [DisplayName("Contact Number")]
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "{0} must be in the format 01X-XXXXXXX")]
    public string ContactNumber { get; set; }
}

public class OrderSlotVM
{
    public string Id { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Slot { get; set; }

    public List<DateOnly> AvailableDates { get; set; } = [];
    public List<TimeOnly> EnabledSlots { get; set; } = [];
    public List<TimeOnly> AvailableSlots { get; set; } = [];
}

public class HomePageVM
{
    public List<Item> TrendingItems { get; set; } = [];
    public List<Category> Categories { get; set; } = [];
    public List<Item> OrderAgainItems { get; set; } = [];
    public List<Item> DailyDiscoverItems { get; set; } = [];
}

public class AdminHomePageVM
{
    public decimal TotalSales { get; set; }
    public decimal TotalSalesStat { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalOrdersStat { get; set; }
    public int TotalCustomers { get; set; }
    public decimal TotalCustomersStat { get; set; }
    public List<string> SalesPerformanceMonths { get; set; } = [];
    public List<int> SalesPerformanceOrders { get; set; } = [];
    public List<string> LoginDevices { get; set; } = [];
    public List<int> LoginDevicesCount { get; set; } = [];
    public int Page { get; set; } = 1;
    public IPagedList<AuditLog> ActivityLogs { get; set; }
}