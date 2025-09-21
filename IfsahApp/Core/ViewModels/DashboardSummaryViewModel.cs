namespace IfsahApp.Core.ViewModels
{
    public class DashboardSummaryViewModel
    {
        public string Month { get; set; }
        public int NumberOfDisclosures { get; set; }
        public int DisclosuresUnderReview { get; set; }
        public int DisclosuresInProcess { get; set; }
        public int CancelledDisclosures { get; set; }
    }
}
