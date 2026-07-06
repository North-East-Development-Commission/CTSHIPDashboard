using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Helpers
{
    public static class ProviderWalletHelper
    {
        public static async Task<ProviderWallet> GetOrCreateAsync(
            ApplicationDbContext context,
            int providerId,
            decimal pendingCreditToExclude = 0m,
            DateTime? timestamp = null)
        {
            ProviderWallet? wallet = await context.ProviderWallets
                .FirstOrDefaultAsync(x => x.ProviderId == providerId);

            if (wallet != null)
            {
                return wallet;
            }

            decimal currentEnrolleeWalletTotal = await context.EnrolleeWallets
                .Where(x => x.Enrollee != null && x.Enrollee.ProviderId == providerId)
                .SumAsync(x => (decimal?)x.Balance) ?? 0m;

            decimal openingBalance = Math.Max(0m, currentEnrolleeWalletTotal - pendingCreditToExclude);
            DateTime transactionTime = timestamp ?? DateTime.UtcNow;

            wallet = new ProviderWallet
            {
                ProviderId = providerId,
                Balance = openingBalance,
                TotalDisbursed = openingBalance,
                LastDisbursedAt = openingBalance > 0m ? transactionTime : null
            };

            context.ProviderWallets.Add(wallet);

            if (openingBalance > 0m)
            {
                wallet.Transactions.Add(new ProviderWalletTransaction
                {
                    Amount = openingBalance,
                    Type = "OpeningBalance",
                    Reference = "Initial provider wallet balance from enrollee wallets",
                    Timestamp = transactionTime
                });
            }

            return wallet;
        }

        public static async Task CreditAsync(
            ApplicationDbContext context,
            int providerId,
            decimal amount,
            string reference,
            DateTime timestamp)
        {
            if (amount <= 0m)
            {
                return;
            }

            ProviderWallet wallet = await GetOrCreateAsync(
                context,
                providerId,
                pendingCreditToExclude: amount,
                timestamp: timestamp);

            wallet.Balance += amount;
            wallet.TotalDisbursed += amount;
            wallet.LastDisbursedAt = timestamp;

            context.ProviderWalletTransactions.Add(new ProviderWalletTransaction
            {
                ProviderWallet = wallet,
                Amount = amount,
                Type = "Disburse",
                Reference = reference,
                Timestamp = timestamp
            });
        }
    }
}
