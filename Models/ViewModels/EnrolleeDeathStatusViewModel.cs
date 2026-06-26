using CTSHIPDashboard.Models.Enums;

namespace CTSHIPDashboard.Models.ViewModels
{
    public class EnrolleeDeathStatusViewModel
    {
        public bool IsDeceased { get; set; }

        public DeathRegisterStatus? RegisterStatus { get; set; }

        public Guid? DeathRegisterId { get; set; }

        public DateTime? DateOfDeath { get; set; }

        public bool IsPendingHmoVerification =>
            IsDeceased && RegisterStatus == DeathRegisterStatus.SubmittedToHmo;

        public bool IsHmoVerified =>
            IsDeceased && RegisterStatus == DeathRegisterStatus.HmoVerified;

        public string StatusText
        {
            get
            {
                if (!IsDeceased)
                {
                    return "Active";
                }

                return RegisterStatus switch
                {
                    DeathRegisterStatus.Draft => "Death Draft",
                    DeathRegisterStatus.SubmittedToHmo => "Death Pending HMO Verification",
                    DeathRegisterStatus.HmoVerified => "Death Verified by HMO",
                    DeathRegisterStatus.HmoRejected => "Death Rejected by HMO",
                    DeathRegisterStatus.Audited => "Deceased / Audited",
                    DeathRegisterStatus.AuditRejected => "Death Audit Rejected",
                    DeathRegisterStatus.Cancelled => "Death Register Cancelled",
                    _ => "Deceased"
                };
            }
        }

        public static EnrolleeDeathStatusViewModel Active()
        {
            return new EnrolleeDeathStatusViewModel
            {
                IsDeceased = false,
                RegisterStatus = null,
                DeathRegisterId = null,
                DateOfDeath = null
            };
        }
    }
}
