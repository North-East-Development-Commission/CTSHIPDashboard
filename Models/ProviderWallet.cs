using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models
{
    public class ProviderWallet
    {
        [Key]
        public int Id { get; set; }
        public int ProviderId { get; set; }
        public decimal Balance { get; set; } = 0m;
        public decimal TotalDisbursed { get; set; } = 0m;
        public DateTime? LastDisbursedAt { get; set; }

        public Provider? Provider { get; set; }
        public ICollection<ProviderWalletTransaction> Transactions { get; set; } = new List<ProviderWalletTransaction>();
    }
}
