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
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }

    [StringLength(100)]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}

public class RegisterVM
{
    [StringLength(50, ErrorMessage = "Name must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(100, ErrorMessage = "Email must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class ForgotPasswordVM
{
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class ChangeEmailVM
{
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Compare("Email", ErrorMessage = "Emails do not match.")]
    [DisplayName("Confirm Email")]
    public string ConfirmEmail { get; set; }
}

public class AccountProfileVM
{
    [StringLength(50, ErrorMessage = "Name must not exceed {0} characters.")]
    public string Name { get; set; }
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "{0} must be in the format 01X-XXXXXXX")]
    [DisplayName("Phone Number")]
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool RemoveImage { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile? Image { get; set; }
}

public class ChangePasswordVM
{
    [StringLength(100)]
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
    public Dictionary<string, string> AvailableSortBy { get; set; } = [];
    public List<Category> AvailableCategories { get; set; } = [];
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
    public IPagedList<Account> Results { get; set; }
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