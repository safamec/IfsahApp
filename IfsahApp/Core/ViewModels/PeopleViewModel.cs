using System.ComponentModel.DataAnnotations;
using IfsahApp.Core.Enums;
using Microsoft.Extensions.Localization;

namespace IfsahApp.Core.ViewModels;

public class PeopleViewModel
{
    private readonly IStringLocalizer<PeopleViewModel> _localizer;

    public PeopleViewModel()
    {
        // Constructor for when DI is not available
    }

    public PeopleViewModel(IStringLocalizer<PeopleViewModel> localizer)
    {
        _localizer = localizer;
    }

    // Helper method to get localized strings
    private string GetLocalizedString(string key)
    {
        return _localizer?[key] ?? key;
    }

    [Required(ErrorMessage = "Name_Required")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email_Invalid")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Phone_Invalid")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Organization")]
    public string? Organization { get; set; }

    [Display(Name = "Type")]
    public PeopleType Type { get; internal set; }

    // Methods to get display names and error messages
    public string GetDisplayName(string propertyName)
    {
        return GetLocalizedString(propertyName);
    }

    public string GetErrorMessage(string errorKey)
    {
        return GetLocalizedString(errorKey);
    }

    // Specific property methods for easier access
    public string DisplayName => GetDisplayName("Name");
    public string DisplayEmail => GetDisplayName("Email");
    public string DisplayPhone => GetDisplayName("Phone");
    public string DisplayOrganization => GetDisplayName("Organization");
    public string DisplayType => GetDisplayName("Type");

    public string NameRequiredError => GetErrorMessage("Name_Required");
    public string EmailInvalidError => GetErrorMessage("Email_Invalid");
    public string PhoneInvalidError => GetErrorMessage("Phone_Invalid");
}