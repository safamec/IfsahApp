namespace IfsahApp.Models;

/// <summary>
/// View model used to display error information, such as the current request ID.
/// </summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}
