using IfsahApp.Core.Enums;

namespace IfsahApp.Core.ViewModels;

public class DisclosureDashboardViewModel
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public DisclosureStatus Status { get; set; } // Keep as enum
    public string Description { get; set; } = string.Empty;
}
