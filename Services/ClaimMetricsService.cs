using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;

namespace CTSHIPDashboard.Services;

public static class ClaimMetricsService
{
    public static ClaimMatrixViewModel Build(IEnumerable<Claim> claims)
    {
        List<Claim> claimList = claims.ToList();

        return new ClaimMatrixViewModel
        {
            TotalClaims = claimList.Count,
            SubmittedClaims = claimList.Count(IsSubmitted),
            ClaimsValidated = claimList.Count(IsValidated),
            QueryClaims = claimList.Count(IsQueried),
            ApprovedClaims = claimList.Count(IsApproved),
            PaidClaims = claimList.Count(IsPaid),
            RejectedClaims = claimList.Count(IsRejected),
            OutstandingClaims = claimList.Count(HasOutstandingBalance),
            TotalClaimAmount = claimList.Sum(claim => claim.Amount),
            ApprovedClaimAmount = claimList.Sum(ApprovedAmountFor),
            PaidClaimAmount = claimList.Sum(claim => claim.AmountPaid),
            OutstandingClaimAmount = claimList.Sum(OutstandingAmountFor),
            AverageProcessingDays = AverageProcessingDaysFor(claimList)
        };
    }

    public static bool IsSubmitted(Claim claim)
    {
        return HasStatus(claim, "Submitted");
    }

    public static bool IsApproved(Claim claim)
    {
        return HasAnyStatus(
            claim,
            "Approved",
            "Partially Approved",
            "Review Approved");
    }

    public static bool IsValidated(Claim claim)
    {
        return IsApproved(claim) || IsPaid(claim);
    }

    public static bool IsPaid(Claim claim)
    {
        return HasStatus(claim, "Paid");
    }

    public static bool IsRejected(Claim claim)
    {
        return HasStatus(claim, "Rejected");
    }

    public static bool IsQueried(Claim claim)
    {
        return HasAnyStatus(
            claim,
            "Queried",
            "Query Raised",
            "Returned for Clarification")
            || claim.ReturnedForClarificationAt.HasValue
            || claim.Queries.Any(query => !HasStatus(query.Status, "Closed"));
    }

    public static decimal ApprovedAmountFor(Claim claim)
    {
        if (claim.AmountApproved > 0m)
        {
            return claim.AmountApproved;
        }

        if (IsValidated(claim))
        {
            decimal approved = claim.Amount - claim.DeductionAmount;
            return Math.Max(approved, 0m);
        }

        return 0m;
    }

    public static decimal OutstandingAmountFor(Claim claim)
    {
        return Math.Max(ApprovedAmountFor(claim) - claim.AmountPaid, 0m);
    }

    public static bool HasOutstandingBalance(Claim claim)
    {
        return OutstandingAmountFor(claim) > 0m;
    }

    public static double ProcessingDaysFor(Claim claim)
    {
        DateTime? closedAt = claim.DatePaid
            ?? claim.DateProcessed
            ?? claim.DateApproved
            ?? claim.DateRejected;

        if (!closedAt.HasValue)
        {
            return 0d;
        }

        return Math.Max((closedAt.Value.Date - claim.DateSubmitted.Date).TotalDays, 0d);
    }

    private static double AverageProcessingDaysFor(IEnumerable<Claim> claims)
    {
        List<double> processingDays = claims
            .Select(ProcessingDaysFor)
            .Where(days => days > 0d)
            .ToList();

        return processingDays.Count == 0
            ? 0d
            : Math.Round(processingDays.Average(), 1);
    }

    private static bool HasAnyStatus(Claim claim, params string[] statuses)
    {
        return statuses.Any(status => HasStatus(claim, status));
    }

    private static bool HasStatus(Claim claim, string status)
    {
        return HasStatus(claim.Status, status);
    }

    private static bool HasStatus(string? actual, string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.OrdinalIgnoreCase);
    }
}
