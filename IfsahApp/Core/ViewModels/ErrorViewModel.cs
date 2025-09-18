namespace IfsahApp.Core.ViewModels
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public int StatusCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDescription { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}