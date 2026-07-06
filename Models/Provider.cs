
namespace CTSHIPDashboard.Models
{
    public class Provider
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Location { get; set; } // State, LGA, Ward for geospatial
        public bool IsActive { get; set; }
        public int PatientRatio { get; set; } // For workforce reporting
        public double? Latitude { get; set; } 
        public double? Longitude { get; set; } 
        public string State { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Level { get; set; }
        public virtual ICollection<Enrollee> Enrollees { get; set; } = new List<Enrollee>();
        public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
        public virtual ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        public ProviderWallet? Wallet { get; set; }
        public DateTime DateRegistered { get; set; }
        public int HmoId { get; set; }
        public string? WalletSource { get; set; }  // <-- new column
    }
}
