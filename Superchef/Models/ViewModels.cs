using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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