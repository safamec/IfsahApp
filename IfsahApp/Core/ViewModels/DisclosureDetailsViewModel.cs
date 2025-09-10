using IfsahApp.Core.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IfsahApp.Core.ViewModels;

public class DisclosureDetailsViewModel
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public DisclosureStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;

    // Assigned user display name
    public string AssignedToUserName { get; set; } = string.Empty;

    // Comments
    public List<CommentViewModel> Comments { get; set; } = new();

    // New comment input
    public string NewComment { get; set; } = string.Empty;

    // Assignment
    public int? AssignToUserId { get; set; }
    public List<SelectListItem> AvailableUsers { get; set; } = new();
}

public class CommentViewModel
{
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
