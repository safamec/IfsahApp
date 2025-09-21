// Core/ViewModels/AddExaminerVM.cs
using System.ComponentModel.DataAnnotations;

public class AddExaminerVM
{
    [Required, Display(Name = "AD User")]
    public string ADUserName { get; set; } = string.Empty;

    [Display(Name = "Full Name")]               // <-- no [Required]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Department")]
    public string? Department { get; set; }
}
