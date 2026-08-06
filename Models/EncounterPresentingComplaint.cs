using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CTSHIPDashboard.Models
{
    public class EncounterPresentingComplaint
    {
        public int Id { get; set; }

        [Required]
        public int EncounterId { get; set; }
        public Encounter? Encounter { get; set; }

        [Required]
        [StringLength(200)]
        public string ComplaintName { get; set; } = string.Empty;
    }
}
