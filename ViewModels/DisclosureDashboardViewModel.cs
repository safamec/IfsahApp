public class DisclosureDashboardViewModel
{
    public string Reference { get; set; }
    public string Type { get; set; }
    public DateTime Date { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
    public string Audit { get; set; }  // ✅ NEW
    public string ActionUrl { get; set; }
    public string EditUrl { get; set; }
}
