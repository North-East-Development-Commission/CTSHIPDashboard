namespace CTSHIPDashboard.Models.ViewModels
{
    public class EnrolleeListViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EnrollmentNumber { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string HmoName { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime DateRegistered { get; set; }
        public string? PhotoPath { get; set; }
        public Int64 NIN { get; internal set; }

        // Read-only computed property — perfect!
        public string StatusBadgeClass => Status?.ToLowerInvariant() switch
        {
            "active" => "bg-success",
            "inactive" => "bg-secondary",
            "suspended" => "bg-danger",
            "terminated" => "bg-dark",
            "pending" => "bg-warning text-dark",
            _ => "bg-light text-dark"
        };

        public string StatusDisplay => Status?.ToLowerInvariant() switch
        {
            "active" => "Active",
            "inactive" => "Inactive",
            "suspended" => "Suspended",
            "terminated" => "Terminated",
            "pending" => "Pending Approval",
            _ => Status ?? "Unknown"
        };
    }
}
