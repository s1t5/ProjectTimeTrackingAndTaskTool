using System.ComponentModel.DataAnnotations;

namespace ProjektZeiterfassung.ViewModels
{
    public class ErrorViewModel
    {
        [Display(Name = "Request ID")]
        public string? RequestId { get; set; }

        [Display(Name = "Error Message")]
        public string? Message { get; set; }

        [Display(Name = "Error Details")]
        public string? StackTrace { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public bool ShowDetails => !string.IsNullOrEmpty(Message);
    }
}