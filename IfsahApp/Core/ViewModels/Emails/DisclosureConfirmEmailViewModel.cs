namespace IfsahApp.Core.ViewModels.Emails
{
    public class DisclosureConfirmEmailViewModel
    {
        public string ReportNumber { get; set; } = "";
        public string ReceivedDate { get; set; } = "";
        public string TrackUrl { get; set; } = "#";

        // جديد: نستخدم رابط صورة مباشر بدل CID
        public string? LogoUrl { get; set; }
    }
}
