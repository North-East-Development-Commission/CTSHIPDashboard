using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services;

internal static class EnrolleeBulkDeletion
{
    public static async Task<(bool Succeeded, string Message)> DeleteAsync(
        ApplicationDbContext context,
        IQueryable<Enrollee> scope,
        int[] ids,
        IAuditService auditService,
        string actor,
        CancellationToken cancellationToken)
    {
        int[] selectedIds = ids.Distinct().ToArray();
        if (selectedIds.Length == 0)
        {
            return (false, "Select at least one enrollee to delete.");
        }

        var selected = scope.Where(enrollee => selectedIds.Contains(enrollee.Id));
        var enrollees = await selected.AsNoTracking()
            .Select(enrollee => new
            {
                enrollee.EnrollmentNumber,
                enrollee.FullName,
                enrollee.HmoId,
                enrollee.ProviderId
            })
            .ToListAsync(cancellationToken);

        if (enrollees.Count != selectedIds.Length)
        {
            return (false, "No enrollees were deleted. One or more selected records are unavailable.");
        }

        try
        {
            bool deleted = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                int count = await selected
                    .Where(enrollee => !enrollee.Encounters.Any() && !enrollee.Claims!.Any())
                    .ExecuteDeleteAsync(cancellationToken);
                if (count != selectedIds.Length)
                {
                    return false;
                }

                await transaction.CommitAsync(cancellationToken);
                return true;
            });

            if (!deleted)
            {
                return (false, "No enrollees were deleted. Check that every selected record is still available and has no encounters or claims.");
            }
        }
        catch (System.Data.Common.DbException)
        {
            return (false, "The deletion could not be completed. Check whether the selected enrollees have linked records.");
        }

        foreach (var enrollee in enrollees)
        {
            await auditService.LogAsync(
                "Enrollee.Deleted",
                actor,
                enrollee.EnrollmentNumber,
                AuditActor.Details($"Name:{enrollee.FullName}", $"HMO:{enrollee.HmoId}", $"Provider:{enrollee.ProviderId}"),
                cancellationToken);
        }

        return (true, $"{enrollees.Count} enrollee(s) deleted permanently.");
    }
}
