using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class ProviderWalletTransaction
    {
        [Key]
        public int Id { get; set; }
        public int ProviderWalletId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Adjustment";
        public string? Reference { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public ProviderWallet? ProviderWallet { get; set; }
    }
}
