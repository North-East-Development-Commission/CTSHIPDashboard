using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class EnrolleeWallet
    {
        [Key]
        public int Id { get; set; }
        public int EnrolleeId { get; set; }
        public decimal Balance { get; set; } = 0m;
        public decimal MonthlyAllocation { get; set; } = 0m; // amount assigned per human life
        public DateTime? LastDisbursedAt { get; set; }

        // Navigation
        public Enrollee? Enrollee { get; set; }
        public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    }
}