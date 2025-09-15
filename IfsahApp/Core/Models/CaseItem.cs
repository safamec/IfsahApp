using IfsahApp.Core.Enums;

namespace IfsahApp.Core.Models
{
    public class CaseItem
{
    public string Type { get; set; } = "";
    public string Reference { get; set; } = "";
    public DateTime Date { get; set; }
    public string Location { get; set; } = "";
    public string Status { get; set; } = ""; // <- string, not enum
    public string Description { get; set; } = "";
}

}
