namespace IfsahApp.Core.ViewModels
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public int StatusCode { get; set; }
        public string ErrorTitle { get; set; } = string.Empty;
        public string ErrorDescription { get; set; } = string.Empty;

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}