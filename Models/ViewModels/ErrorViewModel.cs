namespace CTSHIPDashboard.Models.ViewModels
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public int StatusCode { get; set; } = 500;
        public string Title { get; set; } = "Something went wrong";
        public string Message { get; set; } =
            "We could not complete your request. Please try again.";
        public string Icon { get; set; } = "bi-exclamation-triangle-fill";

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
