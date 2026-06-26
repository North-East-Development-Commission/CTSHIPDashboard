using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class EncounterService
    {
        public int Id { get; set; }
        public int EncounterId { get; set; }
        public Encounter? Encounter { get; set; }

        [Required, StringLength(50)]
        public string ServiceSetting { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string ServiceName { get; set; } = string.Empty;
    }
}
