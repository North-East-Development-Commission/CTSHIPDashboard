using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class HmoBulkDisbursementViewModel
    {
        [Required(ErrorMessage = "Enter the amount to disburse to each enrollee.")]
        [Range(typeof(decimal), "0.01", "999999999999.99", ErrorMessage = "Amount per enrollee must be greater than zero.")]
        [Display(Name = "Amount per Enrollee (NGN)")]
        public decimal AmountPerEnrollee { get; set; }

        [Required(ErrorMessage = "Select the enrollee status to fund.")]
        [StringLength(20)]
        [Display(Name = "Enrollee Status")]
        public string Status { get; set; } = "Active";

        [Required(ErrorMessage = "Select a disbursement category.")]
        [StringLength(50)]
        [Display(Name = "Disbursement Category")]
        public string Category { get; set; } = "Monthly Allocation";

        public string HmoName { get; set; } = string.Empty;
        public List<string> CategoryOptions { get; set; } = new();
        public List<HmoDisbursementStatusOptionViewModel> StatusOptions { get; set; } = new();
    }

    public class HmoDisbursementStatusOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int EligibleCount { get; set; }
    }
}
