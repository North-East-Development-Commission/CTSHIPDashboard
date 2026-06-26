using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class ProgramMonitoringTarget
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Scope { get; set; } = "National";

        [Range(1, int.MaxValue)]
        public int TargetEnrollees { get; set; }

        public DateTime UpdatedAt { get; set; }

        [StringLength(450)]
        public string? UpdatedByUserId { get; set; }

        [StringLength(200)]
        public string? UpdatedByName { get; set; }
    }
}
