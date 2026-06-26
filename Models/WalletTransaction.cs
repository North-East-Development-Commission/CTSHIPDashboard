using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; }
        public int EnrolleeWalletId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Adjustment"; // Disburse, Deduction, Adjustment
        public string? Reference { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public EnrolleeWallet? EnrolleeWallet { get; set; }
    }
}