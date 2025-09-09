namespace IfsahApp.ViewModels
{
    public class DisclosureDashboardViewModel
    {
        public int Id { get; set; }                     // ✅ Needed by DashboardController
        public string Reference { get; set; } = "";     // DisclosureNumber
        public string Type { get; set; } = "";          // DisclosureType.EnglishName or ArabicName
        public DateTime Date { get; set; }
        public string Location { get; set; } = "";
        public string Status { get; set; } = "";        // Localized enum string
        public string Description { get; set; } = "";   // ✅ Needed by DashboardController
        public string ActionUrl { get; set; } = "";
        public string EditUrl { get; set; } = "";
    }
}
