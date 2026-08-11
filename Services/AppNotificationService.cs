using CTSHIPDashboard.Data;
using CTSHIPDashboard.Hubs;
using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CTSHIPDashboard.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<AnalyticsHub> _hubContext;
    private readonly ILogger<AppNotificationService> _logger;

    public AppNotificationService(
        ApplicationDbContext context,
        IHubContext<AnalyticsHub> hubContext,
        ILogger<AppNotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }
    public async Task NotifyEncounterSubmittedAsync(
        int encounterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var encounter = await _context.Encounters
                .AsNoTracking()
                .Where(x => x.Id == encounterId)
                .Select(x => new
                {
                    x.Id,
                    x.EncounterNumber,
                    x.VisitDate,
                    x.Status,
                    x.ServiceSetting,
                    x.ProviderId,
                    ProviderName = x.Provider == null ? "Provider" : x.Provider.Name,
                    EnrolleeName = x.Enrollee == null ? "enrollee" : x.Enrollee.FullName,
                    EnrolleeNumber = x.Enrollee == null ? string.Empty : x.Enrollee.EnrollmentNumber,
                    HmoId = x.Enrollee == null ? (int?)null : x.Enrollee.HmoId,
                    HmoName = x.Enrollee == null || x.Enrollee.Hmo == null ? "the HMO" : x.Enrollee.Hmo.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (encounter == null) return;

            var payload = new
            {
                Type = "EncounterSubmitted",
                Title = "Encounter received",
                Message = $"{encounter.ProviderName} submitted encounter {encounter.EncounterNumber} for {encounter.EnrolleeName}.",
                Url = $"/Hmo/EncDetails/{encounter.Id}",
                Icon = "info",
                encounter.Id,
                encounter.EncounterNumber,
                encounter.EnrolleeName,
                encounter.EnrolleeNumber,
                encounter.ProviderName,
                encounter.HmoName,
                encounter.VisitDate,
                encounter.Status,
                encounter.ServiceSetting
            };

            if (encounter.HmoId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Hmo(encounter.HmoId.Value),
                    "EncounterSubmitted",
                    payload,
                    cancellationToken);
            }

            await SendNotificationAsync(
                NotificationGroups.Role("IHSA"),
                "EncounterSubmitted",
                payload,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send encounter notification for encounter {EncounterId}.",
                encounterId);
        }
    }


    public async Task NotifyReferralInitiatedAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var referral = await _context.Referrals
                .AsNoTracking()
                .Include(x => x.ReferredHospital)
                .Where(x => x.Id == referralId && !x.IsDeleted)
                .Select(x => new
                {
                    x.Id,
                    x.EnrolleeFullName,
                    x.EnrolleeNumber,
                    x.FromProviderId,
                    x.FromProviderName,
                    x.HmoCode,
                    x.HmoName,
                    x.ReferredHospitalId,
                    ReferredHospitalName = x.ReferredHospital == null
                        ? "referred facility"
                        : x.ReferredHospital.Name,
                    ReferredHospitalEmail = x.ReferredHospital == null
                        ? null
                        : x.ReferredHospital.Email
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (referral == null)
            {
                return;
            }

            string? hmoGroup = await ResolveHmoGroupAsync(referral.HmoCode, cancellationToken);

            if (!string.IsNullOrWhiteSpace(hmoGroup))
            {
                await SendNotificationAsync(
                    hmoGroup,
                    "ReferralInitiated",
                    new
                    {
                        Type = "ReferralInitiated",
                        Title = "Referral awaiting HMO action",
                        Message = $"{referral.FromProviderName} initiated a referral for {referral.EnrolleeFullName}. Review and verify it.",
                        Url = $"/Hmos/Referrals/Details/{referral.Id}",
                        Icon = "info",
                        referral.Id,
                        referral.EnrolleeFullName,
                        referral.EnrolleeNumber,
                        referral.FromProviderName,
                        referral.HmoName,
                        referral.ReferredHospitalName
                    },
                    cancellationToken);
            }

            if (TryParseProviderId(referral.FromProviderId, out int providerId))
            {
                await SendNotificationAsync(
                    NotificationGroups.Provider(providerId),
                    "ReferralInitiated",
                    new
                    {
                        Type = "ReferralInitiated",
                        Title = "Referral initiated",
                        Message = $"Referral for {referral.EnrolleeFullName} has been submitted to {referral.HmoName ?? "the HMO"}.",
                        Url = $"/Providers/Referrals/Details/{referral.Id}",
                        Icon = "success",
                        referral.Id,
                        referral.EnrolleeFullName,
                        referral.EnrolleeNumber,
                        referral.FromProviderName,
                        referral.HmoName,
                        referral.ReferredHospitalName
                    },
                    cancellationToken);
            }

            await SendNotificationAsync(
                NotificationGroups.ReferralHospital(referral.ReferredHospitalId),
                "ReferralInitiated",
                new
                {
                    Type = "ReferralInitiated",
                    Title = "Incoming referral",
                    Message = $"{referral.FromProviderName} initiated a referral for {referral.EnrolleeFullName} to your facility.",
                    Url = $"/ReferralPro/Referrals/Details/{referral.Id}",
                    Icon = "info",
                    referral.Id,
                    referral.EnrolleeFullName,
                    referral.EnrolleeNumber,
                    referral.FromProviderName,
                    referral.HmoName,
                    referral.ReferredHospitalName
                },
                cancellationToken);

            int? secondaryProviderId = await ResolveSecondaryProviderIdAsync(
                referral.ReferredHospitalName,
                referral.ReferredHospitalEmail,
                cancellationToken);

            if (secondaryProviderId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Provider(secondaryProviderId.Value),
                    "ReferralInitiated",
                    new
                    {
                        Type = "ReferralInitiated",
                        Title = "Incoming referral",
                        Message = $"{referral.FromProviderName} submitted a referral for {referral.EnrolleeFullName} to your facility.",
                        Url = $"/ReferralPro/Referrals/Details/{referral.Id}",
                        Icon = "info",
                        referral.Id,
                        referral.EnrolleeFullName,
                        referral.EnrolleeNumber,
                        referral.FromProviderName,
                        referral.HmoName,
                        referral.ReferredHospitalName
                    },
                    cancellationToken);
            }

            var oversightPayload = new
            {
                Type = "ReferralInitiated",
                Title = "Referral submitted",
                Message = $"{referral.FromProviderName} submitted a referral for {referral.EnrolleeFullName} to {referral.ReferredHospitalName}.",
                Url = $"/IHSA/ReferralDetails/{referral.Id}",
                Icon = "info",
                referral.Id,
                referral.EnrolleeFullName,
                referral.EnrolleeNumber,
                referral.FromProviderName,
                referral.HmoName,
                referral.ReferredHospitalName
            };

            await SendNotificationAsync(NotificationGroups.Role("IHSA"), "ReferralInitiated", oversightPayload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("Admin"), "ReferralInitiated", oversightPayload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("CTSHIPAdmin"), "ReferralInitiated", oversightPayload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send referral notification for referral {ReferralId}.",
                referralId);
        }
    }

    public async Task NotifyClaimSubmittedAsync(
        int claimId,
        Guid? referralId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var claim = await _context.Claims
                .AsNoTracking()
                .Include(x => x.Enrollee)
                .Include(x => x.Hmos)
                .Include(x => x.Provider)
                .Where(x => x.Id == claimId)
                .Select(x => new
                {
                    x.Id,
                    x.ClaimNumber,
                    x.Amount,
                    x.Status,
                    x.HmoId,
                    HmoName = x.Hmos == null ? "the HMO" : x.Hmos.Name,
                    ProviderId = x.ProviderId,
                    ProviderName = x.Provider == null ? "Provider" : x.Provider.Name,
                    EnrolleeName = x.Enrollee == null ? "enrollee" : x.Enrollee.FullName,
                    EnrolleeNumber = x.Enrollee == null ? string.Empty : x.Enrollee.EnrollmentNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (claim == null)
            {
                return;
            }

            if (claim.HmoId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Hmo(claim.HmoId.Value),
                    "ClaimSubmitted",
                    new
                    {
                        Type = "ClaimSubmitted",
                        Title = "Claim awaiting HMO review",
                        Message = $"{claim.ProviderName} submitted claim {claim.ClaimNumber} for {claim.EnrolleeName}.",
                        Url = $"/Claims/Details/{claim.Id}",
                        Icon = "info",
                        claim.Id,
                        claim.ClaimNumber,
                        claim.EnrolleeName,
                        claim.EnrolleeNumber,
                        claim.HmoName,
                        claim.ProviderName,
                        claim.Amount,
                        claim.Status
                    },
                    cancellationToken);
            }

            await SendNotificationAsync(
                NotificationGroups.Provider(claim.ProviderId),
                "ClaimSubmitted",
                new
                {
                    Type = "ClaimSubmitted",
                    Title = "Claim submitted",
                    Message = $"Claim {claim.ClaimNumber} has been submitted to {claim.HmoName}.",
                    Url = $"/Providers/ClaimDetails/{claim.Id}",
                    Icon = "success",
                    claim.Id,
                    claim.ClaimNumber,
                    claim.EnrolleeName,
                    claim.EnrolleeNumber,
                    claim.HmoName,
                    claim.ProviderName,
                    claim.Amount,
                    claim.Status
                },
                cancellationToken);

            if (referralId.HasValue)
            {
                Guid? hospitalId = await _context.Referrals
                    .AsNoTracking()
                    .Where(x => x.Id == referralId.Value && !x.IsDeleted)
                    .Select(x => (Guid?)x.ReferredHospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (hospitalId.HasValue)
                {
                    await SendNotificationAsync(
                        NotificationGroups.ReferralHospital(hospitalId.Value),
                        "ClaimSubmitted",
                        new
                        {
                            Type = "ClaimSubmitted",
                            Title = "Referral claim submitted",
                            Message = $"Claim {claim.ClaimNumber} has been submitted to {claim.HmoName}.",
                            Url = $"/ReferralPro/Referrals/Details/{referralId.Value}",
                            Icon = "success",
                            claim.Id,
                            claim.ClaimNumber,
                            claim.EnrolleeName,
                            claim.EnrolleeNumber,
                            claim.HmoName,
                            claim.ProviderName,
                            claim.Amount,
                            claim.Status,
                            ReferralId = referralId.Value
                        },
                        cancellationToken);
                }
            }

            var oversightPayload = new
            {
                Type = "ClaimSubmitted",
                Title = "Claim submitted",
                Message = $"{claim.ProviderName} submitted claim {claim.ClaimNumber} for {claim.EnrolleeName}.",
                Url = $"/Claims/Details/{claim.Id}",
                Icon = "info",
                claim.Id,
                claim.ClaimNumber,
                claim.EnrolleeName,
                claim.EnrolleeNumber,
                claim.HmoName,
                claim.ProviderName,
                claim.Amount,
                claim.Status
            };

            await SendNotificationAsync(NotificationGroups.Role("IHSA"), "ClaimSubmitted", oversightPayload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("Admin"), "ClaimSubmitted", oversightPayload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("CTSHIPAdmin"), "ClaimSubmitted", oversightPayload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not send claim notification for claim {ClaimId}.",
                claimId);
        }
    }

    public async Task NotifyComplaintSubmittedAsync(
        int complaintId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var complaint = await _context.Complaints
                .AsNoTracking()
                .Include(x => x.Hmo)
                .Include(x => x.Provider)
                .Where(x => x.Id == complaintId)
                .Select(x => new
                {
                    x.Id,
                    x.ReferenceNumber,
                    x.Subject,
                    x.Priority,
                    x.State,
                    x.HmoId,
                    HmoName = x.Hmo == null ? null : x.Hmo.Name,
                    x.ProviderId,
                    ProviderName = x.Provider == null ? null : x.Provider.Name,
                    x.SubmittedByName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (complaint == null)
            {
                return;
            }

            var payload = new
            {
                Type = "ComplaintSubmitted",
                Title = "New complaint submitted",
                Message = $"{complaint.ReferenceNumber}: {complaint.Subject}",
                Url = $"/Complaints/Details/{complaint.Id}",
                Icon = complaint.Priority.ToString() == "Critical" ? "warning" : "info",
                complaint.Id,
                complaint.ReferenceNumber,
                complaint.Subject,
                complaint.Priority,
                complaint.State,
                complaint.HmoName,
                complaint.ProviderName,
                complaint.SubmittedByName
            };

            if (complaint.ProviderId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Provider(complaint.ProviderId.Value),
                    "ComplaintSubmitted",
                    payload,
                    cancellationToken);
            }

            if (complaint.HmoId.HasValue)
            {
                await SendNotificationAsync(
                    NotificationGroups.Hmo(complaint.HmoId.Value),
                    "ComplaintSubmitted",
                    payload,
                    cancellationToken);
            }

            await SendNotificationAsync(NotificationGroups.Role("Admin"), "ComplaintSubmitted", payload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("CTSHIPAdmin"), "ComplaintSubmitted", payload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send complaint notification for complaint {ComplaintId}.", complaintId);
        }
    }

    public async Task NotifyMonthlyReportSubmittedAsync(
        int reportId,
        bool isReferralProviderReport = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _context.StateOfficeMonthlyReports
                .AsNoTracking()
                .Where(x => x.Id == reportId)
                .Select(x => new
                {
                    x.Id,
                    x.State,
                    x.FacilityName,
                    x.FacilityCode,
                    x.ReportingMonth,
                    x.TotalClaims,
                    x.TotalEncounters,
                    x.TotalReferrals
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (report == null)
            {
                return;
            }

            string reportType = isReferralProviderReport ? "Referral provider report" : "Provider monthly report";
            string url = isReferralProviderReport
                ? $"/IHSA/ReferralProviderReportDetails/{report.Id}"
                : $"/IHSA/MonthlyReportDetails/{report.Id}";

            var payload = new
            {
                Type = "MonthlyReportSubmitted",
                Title = reportType + " submitted",
                Message = $"{report.FacilityName} submitted {report.ReportingMonth:MMMM yyyy} report for {report.State}.",
                Url = url,
                Icon = "info",
                report.Id,
                report.State,
                report.FacilityName,
                report.FacilityCode,
                report.ReportingMonth,
                report.TotalClaims,
                report.TotalEncounters,
                report.TotalReferrals,
                IsReferralProviderReport = isReferralProviderReport
            };

            await SendNotificationAsync(NotificationGroups.Role("IHSA"), "MonthlyReportSubmitted", payload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("Admin"), "MonthlyReportSubmitted", payload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("CTSHIPAdmin"), "MonthlyReportSubmitted", payload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send monthly report submission notification for report {ReportId}.", reportId);
        }
    }

    public async Task NotifyMonthlyReportAuditedAsync(
        int reportId,
        bool isReferralProviderReport = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _context.StateOfficeMonthlyReports
                .AsNoTracking()
                .Where(x => x.Id == reportId)
                .Select(x => new
                {
                    x.Id,
                    x.State,
                    x.FacilityName,
                    x.FacilityCode,
                    x.ReportingMonth,
                    x.AuditStatus,
                    x.AuditedByName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (report == null)
            {
                return;
            }

            string reportType = isReferralProviderReport ? "Referral provider report" : "Provider monthly report";
            string url = isReferralProviderReport
                ? $"/StateOffice/ReferralProviderReportDetails/{report.Id}"
                : $"/StateOffice/MonthlyReportDetails/{report.Id}";

            var payload = new
            {
                Type = "MonthlyReportAudited",
                Title = reportType + " audited",
                Message = $"IHSA audit marked {report.FacilityName} report as {report.AuditStatus}.",
                Url = url,
                Icon = report.AuditStatus == "Audited" ? "success" : "warning",
                report.Id,
                report.State,
                report.FacilityName,
                report.FacilityCode,
                report.ReportingMonth,
                report.AuditStatus,
                report.AuditedByName,
                IsReferralProviderReport = isReferralProviderReport
            };

            await SendNotificationAsync(NotificationGroups.Role("Admin"), "MonthlyReportAudited", payload, cancellationToken);
            await SendNotificationAsync(NotificationGroups.Role("CTSHIPAdmin"), "MonthlyReportAudited", payload, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send monthly report audit notification for report {ReportId}.", reportId);
        }
    }

    private async Task<string?> ResolveHmoGroupAsync(
        string? hmoCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hmoCode))
        {
            return null;
        }

        int? hmoId = await _context.Hmos
            .AsNoTracking()
            .Where(x => x.RegistrationNumber == hmoCode.Trim())
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return hmoId.HasValue
            ? NotificationGroups.Hmo(hmoId.Value)
            : NotificationGroups.HmoCode(hmoCode);
    }

    private async Task SendNotificationAsync(
        string group,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        int? notificationId = await StoreNotificationAsync(group, eventName, payload, cancellationToken);

        await _hubContext.Clients.Group(group).SendAsync(
            "AppNotification",
            payload,
            cancellationToken);

        await _hubContext.Clients.Group(group).SendAsync(
            eventName,
            payload,
            cancellationToken);

        await _hubContext.Clients.Group(group).SendAsync(
            "NotificationCountChanged",
            new { NotificationId = notificationId },
            cancellationToken);
    }

    private async Task<int?> StoreNotificationAsync(
        string group,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            string json = JsonSerializer.Serialize(payload);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            AppNotification notification = new()
            {
                TargetGroup = group,
                EventName = eventName,
                Title = GetString(root, "Title") ?? eventName,
                Message = GetString(root, "Message") ?? string.Empty,
                Url = GetString(root, "Url"),
                Icon = GetString(root, "Icon") ?? "info",
                PayloadJson = json,
                CreatedAt = DateTime.UtcNow
            };

            _context.AppNotifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);
            return notification.Id;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not persist notification for group {Group} and event {EventName}.", group, eventName);
            return null;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static bool TryParseProviderId(string? value, out int providerId)
    {
        return int.TryParse(value, out providerId) && providerId > 0;
    }

    private async Task<int?> ResolveSecondaryProviderIdAsync(
        string? referredHospitalName,
        string? referredHospitalEmail,
        CancellationToken cancellationToken)
    {
        string? normalizedName = Normalize(referredHospitalName);
        string? normalizedEmail = Normalize(referredHospitalEmail);

        if (normalizedName == null && normalizedEmail == null)
        {
            return null;
        }

        return await _context.Providers
            .AsNoTracking()
            .Where(x =>
                x.Level != null &&
                x.Level.Contains("Secondary") &&
                ((normalizedName != null && x.Name.ToUpper() == normalizedName) ||
                 (normalizedEmail != null && x.Email != null && x.Email.ToUpper() == normalizedEmail)))
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }
}


