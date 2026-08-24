using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models.Enums
{
    public enum ComplaintCategory
    {
        [Display(Name = "Enrolment problems")]
        Enrollment = 1,

        [Display(Name = "Unavailable medicines/services")]
        ServiceDelivery = 2,

        [Display(Name = "Provider attitude")]
        ProviderConduct = 3,

        [Display(Name = "FFS/claims disputes")]
        Claims = 4,

        [Display(Name = "Delayed payment")]
        Payment = 5,

        [Display(Name = "Delayed referral/authorization")]
        Referral = 6,

        [Display(Name = "Fraud/abuse allegation")]
        DataQuality = 7,

        [Display(Name = "Other complaints")]
        Other = 8,

        [Display(Name = "Denial of care")]
        DenialOfCare = 9,

        [Display(Name = "Demand for unauthorized payment")]
        UnauthorizedPaymentDemand = 10,

        [Display(Name = "Poor quality of care")]
        PoorQualityOfCare = 11,

        [Display(Name = "Capitation disputes")]
        CapitationDisputes = 12
    }
}