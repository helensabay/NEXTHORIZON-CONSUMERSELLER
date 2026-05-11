using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.ViewModels;

public class BecomeSellerViewModel
{
    [Required(ErrorMessage = "Business type is required")]
    public string BusinessType { get; set; } = string.Empty;

    public IFormFile? BrandLogo { get; set; }

    [Required(ErrorMessage = "DTI certificate is required")]
    public IFormFile? DtiCertificate { get; set; }

    [Required(ErrorMessage = "BIR certificate is required")]
    public IFormFile? BirCertificate { get; set; }

    [Required(ErrorMessage = "Business permit is required")]
    public IFormFile? BusinessPermit { get; set; }

    public IFormFile? AdditionalDocument { get; set; }

    [Required(ErrorMessage = "Business Name is required")]
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business Email is required")]
    [Display(Name = "Business Email")]
    [EmailAddress]
    public string BusinessEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business Phone is required")]
    [Display(Name = "Business Phone")]
    public string BusinessPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tax ID is required")]
    [Display(Name = "Tax ID")]
    public string TaxId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business Address is required")]
    [Display(Name = "Business Address")]
    public string BusinessAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "First Name is required")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Last Name is required")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone Number is required")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms of Service")]
    public bool AgreeTerms { get; set; }
}
