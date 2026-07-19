using CTSHIPDashboard.Models;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.ViewModels;
using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CTSHIPDashboard.Services;

public class ReferralService : IReferralService
{
    private const int ReferralVerificationCodeValidDays = 7;

    private readonly ApplicationDbContext _context;
    private readonly IAppNotificationService _notificationService;
    private readonly IAuditService _auditService;

    public ReferralService(
        ApplicationDbContext context,
        IAppNotificationService notificationService,
        IAuditService auditService)
    {
        _context = context;
        _notificationService = notificationService;
        _auditService = auditService;
    }

    public async Task<List<ReferralIndexViewModel>> GetProviderReferralsAsync(string? providerId, string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Referral> query = _context.Referrals
            .AsNoTracking()
            .Include(x => x.ReferredHospital)
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(providerId))
        {
            query = query.Where(x => x.FromProviderId == providerId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => x.EnrolleeNumber.Contains(term) ||
                                     x.EnrolleeFullName.Contains(term) ||
                                     x.FromProviderName.Contains(term) ||
                                     x.Diagnosis.Contains(term) ||
                                     (x.ReferredHospital != null && x.ReferredHospital.Name.Contains(term)) ||
                                     (x.HmoName != null && x.HmoName.Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ReferralIndexViewModel
            {
                Id = x.Id,
                EnrolleeNumber = x.EnrolleeNumber,
                EnrolleeFullName = x.EnrolleeFullName,
                FromProviderName = x.FromProviderName,
                ReferredHospitalName = x.ReferredHospital == null ? string.Empty : x.ReferredHospital.Name,
                HmoName = x.HmoName,
                Diagnosis = x.Diagnosis,
                Priority = x.Priority,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SubmittedToHmoAt = x.SubmittedToHmoAt,
                VerifiedAt = x.VerifiedAt,
                AuditedAt = x.AuditedAt,
                ReferralVerificationCodeExpiresAt = x.ReferralVerificationCodeExpiresAt,
                ReferralVerificationCodeVerifiedAt = x.ReferralVerificationCodeVerifiedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ReferralIndexViewModel>> GetHmoReferralsAsync(string? hmoCode, string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Referral> query = _context.Referrals
            .AsNoTracking()
            .Include(x => x.ReferredHospital)
            .Where(x => !x.IsDeleted && x.Status != ReferralStatus.Draft);

        if (!string.IsNullOrWhiteSpace(hmoCode))
        {
            query = query.Where(x => x.HmoCode == hmoCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => x.EnrolleeNumber.Contains(term) ||
                                     x.EnrolleeFullName.Contains(term) ||
                                     x.FromProviderName.Contains(term) ||
                                     x.Diagnosis.Contains(term) ||
                                     (x.ReferredHospital != null && x.ReferredHospital.Name.Contains(term)) ||
                                     (x.HmoName != null && x.HmoName.Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.SubmittedToHmoAt ?? x.CreatedAt)
            .Select(x => new ReferralIndexViewModel
            {
                Id = x.Id,
                EnrolleeNumber = x.EnrolleeNumber,
                EnrolleeFullName = x.EnrolleeFullName,
                FromProviderName = x.FromProviderName,
                ReferredHospitalName = x.ReferredHospital == null ? string.Empty : x.ReferredHospital.Name,
                HmoName = x.HmoName,
                Diagnosis = x.Diagnosis,
                Priority = x.Priority,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SubmittedToHmoAt = x.SubmittedToHmoAt,
                VerifiedAt = x.VerifiedAt,
                AuditedAt = x.AuditedAt,
                ReferralVerificationCodeExpiresAt = x.ReferralVerificationCodeExpiresAt,
                ReferralVerificationCodeVerifiedAt = x.ReferralVerificationCodeVerifiedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ReferralDetailsViewModel?> GetReferralDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Referral? referral = await _context.Referrals
            .AsNoTracking()
            .Include(x => x.ReferredHospital)
            .Include(x => x.AuditLogs)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (referral == null)
        {
            return null;
        }

        return new ReferralDetailsViewModel
        {
            Id = referral.Id,
            EncounterId = referral.EncounterId,
            EncounterReference = referral.EncounterReference,
            EnrolleeNumber = referral.EnrolleeNumber,
            EnrolleeFullName = referral.EnrolleeFullName,
            HmoCode = referral.HmoCode,
            HmoName = referral.HmoName,
            FromProviderId = referral.FromProviderId,
            FromProviderName = referral.FromProviderName,
            ReferredHospitalName = referral.ReferredHospital == null ? string.Empty : referral.ReferredHospital.Name,
            ReferredHospitalAddress = referral.ReferredHospital == null ? null : referral.ReferredHospital.Address,
            Diagnosis = referral.Diagnosis,
            ReasonForReferral = referral.ReasonForReferral,
            ClinicalSummary = referral.ClinicalSummary,
            TreatmentGiven = referral.TreatmentGiven,
            InvestigationSummary = referral.InvestigationSummary,
            Priority = referral.Priority,
            Status = referral.Status,
            CreatedByName = referral.CreatedByName,
            CreatedAt = referral.CreatedAt,
            SubmittedToHmoAt = referral.SubmittedToHmoAt,
            VerifiedByName = referral.VerifiedByName,
            VerifiedAt = referral.VerifiedAt,
            HmoVerificationNote = referral.HmoVerificationNote,
            ReferralVerificationCode = referral.ReferralVerificationCode,
            ReferralVerificationCodeIssuedAt = referral.ReferralVerificationCodeIssuedAt,
            ReferralVerificationCodeExpiresAt = referral.ReferralVerificationCodeExpiresAt,
            ReferralVerificationCodeIssuedByName = referral.ReferralVerificationCodeIssuedByName,
            ReferralVerificationCodeVerifiedAt = referral.ReferralVerificationCodeVerifiedAt,
            ReferralVerificationCodeVerifiedByName = referral.ReferralVerificationCodeVerifiedByName,
            AuditedByName = referral.AuditedByName,
            AuditedAt = referral.AuditedAt,
            AuditNote = referral.AuditNote,
            AuditLogs = referral.AuditLogs
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new ReferralAuditLogViewModel
                {
                    Action = x.Action,
                    PerformedByName = x.PerformedByName,
                    Note = x.Note,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };
    }

    public async Task<ReferralCreateViewModel> BuildCreateViewModelAsync(ReferralCreateViewModel? model = null, CancellationToken cancellationToken = default)
    {
        ReferralCreateViewModel viewModel = model ?? new ReferralCreateViewModel();
        viewModel.ReferredHospitals = await GetHospitalSelectListAsync(viewModel.ReferredHospitalId, cancellationToken);
        return viewModel;
    }

    public async Task<bool> IsActiveReferralHospitalAsync(Guid? hospitalId, CancellationToken cancellationToken = default)
    {
        return hospitalId.HasValue
            && await _context.ReferralHospitals
                .AsNoTracking()
                .AnyAsync(x => x.Id == hospitalId.Value && x.IsActive, cancellationToken);
    }

    public async Task<Guid> CreateReferralAsync(ReferralCreateViewModel model, string? userId, string? userName, bool submitToHmo, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveReferralHospitalAsync(model.ReferredHospitalId, cancellationToken))
        {
            throw new InvalidOperationException("Select an active referral hospital before saving the referral.");
        }

        Guid referredHospitalId = model.ReferredHospitalId.GetValueOrDefault();

        Referral referral = new Referral
        {
            EncounterId = model.EncounterId,
            EncounterReference = model.EncounterReference,
            EnrolleeId = model.EnrolleeId,
            EnrolleeNumber = model.EnrolleeNumber.Trim(),
            EnrolleeFullName = model.EnrolleeFullName.Trim(),
            HmoCode = string.IsNullOrWhiteSpace(model.HmoCode) ? null : model.HmoCode.Trim(),
            HmoName = string.IsNullOrWhiteSpace(model.HmoName) ? null : model.HmoName.Trim(),
            FromProviderId = string.IsNullOrWhiteSpace(model.FromProviderId) ? null : model.FromProviderId.Trim(),
            FromProviderName = model.FromProviderName.Trim(),
            ReferredHospitalId = referredHospitalId,
            Diagnosis = model.Diagnosis.Trim(),
            ReasonForReferral = model.ReasonForReferral.Trim(),
            ClinicalSummary = string.IsNullOrWhiteSpace(model.ClinicalSummary) ? null : model.ClinicalSummary.Trim(),
            TreatmentGiven = string.IsNullOrWhiteSpace(model.TreatmentGiven) ? null : model.TreatmentGiven.Trim(),
            InvestigationSummary = string.IsNullOrWhiteSpace(model.InvestigationSummary) ? null : model.InvestigationSummary.Trim(),
            Priority = model.Priority,
            Status = submitToHmo ? ReferralStatus.SubmittedToHmo : ReferralStatus.Draft,
            CreatedByUserId = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow,
            SubmittedByUserId = submitToHmo ? userId : null,
            SubmittedToHmoAt = submitToHmo ? DateTime.UtcNow : null
        };

        _context.Referrals.Add(referral);
        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.Created,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = "Referral created."
        });

        if (submitToHmo)
        {
            _context.ReferralAuditLogs.Add(new ReferralAuditLog
            {
                ReferralId = referral.Id,
                Action = ReferralAuditAction.SubmittedToHmo,
                PerformedByUserId = userId,
                PerformedByName = userName,
                Note = "Referral submitted to HMO for verification."
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            submitToHmo ? "Referral.CreatedAndSubmitted" : "Referral.Created",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"FromProvider:{referral.FromProviderName}",
                $"HMO:{referral.HmoName}",
                $"Status:{referral.Status}",
                $"ReferredHospital:{referral.ReferredHospitalId}"),
            cancellationToken);
        if (submitToHmo)
        {
            await _notificationService.NotifyReferralInitiatedAsync(referral.Id, cancellationToken);
        }

        return referral.Id;
    }

    public async Task<bool> SubmitReferralToHmoAsync(Guid referralId, string? userId, string? userName, CancellationToken cancellationToken = default)
    {
        Referral? referral = await _context.Referrals.FirstOrDefaultAsync(x => x.Id == referralId && !x.IsDeleted, cancellationToken);

        if (referral == null || referral.Status != ReferralStatus.Draft)
        {
            return false;
        }

        referral.Status = ReferralStatus.SubmittedToHmo;
        referral.SubmittedByUserId = userId;
        referral.SubmittedToHmoAt = DateTime.UtcNow;

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.SubmittedToHmo,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = "Referral submitted to HMO for verification."
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            "Referral.Submitted",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"FromProvider:{referral.FromProviderName}",
                $"HMO:{referral.HmoName}",
                $"Status:{referral.Status}"),
            cancellationToken);
        return true;
    }

    public async Task<bool> VerifyReferralAsync(ReferralVerificationViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default)
    {
        Referral? referral = await _context.Referrals.FirstOrDefaultAsync(x => x.Id == model.ReferralId && !x.IsDeleted, cancellationToken);

        if (referral == null || referral.Status != ReferralStatus.SubmittedToHmo)
        {
            return false;
        }

        referral.Status = model.IsApproved ? ReferralStatus.Verified : ReferralStatus.Rejected;
        referral.VerifiedByUserId = userId;
        referral.VerifiedByName = userName;
        referral.VerifiedAt = DateTime.UtcNow;
        referral.HmoVerificationNote = model.VerificationNote.Trim();

        if (model.IsApproved)
        {
            await IssueReferralVerificationCodeAsync(referral, userId, userName, cancellationToken);
        }
        else
        {
            ClearReferralVerificationCode(referral);
        }

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = model.IsApproved ? ReferralAuditAction.Verified : ReferralAuditAction.Rejected,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = model.VerificationNote.Trim()
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            model.IsApproved ? "Referral.Verified" : "Referral.Rejected",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"FromProvider:{referral.FromProviderName}",
                $"HMO:{referral.HmoName}",
                $"Status:{referral.Status}",
                $"Note:{model.VerificationNote}"),
            cancellationToken);
        await _notificationService.NotifyReferralInitiatedAsync(referral.Id, cancellationToken);
        return true;
    }

    public async Task<bool> ReissueReferralVerificationCodeAsync(
        Guid referralId,
        string? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        Referral? referral = await _context.Referrals.FirstOrDefaultAsync(
            x => x.Id == referralId && !x.IsDeleted,
            cancellationToken);

        if (referral == null ||
            (referral.Status != ReferralStatus.Verified && referral.Status != ReferralStatus.Audited))
        {
            return false;
        }

        await IssueReferralVerificationCodeAsync(referral, userId, userName, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            "Referral.CodeReissued",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"Status:{referral.Status}",
                referral.ReferralVerificationCodeExpiresAt.HasValue
                    ? $"Expires:{referral.ReferralVerificationCodeExpiresAt.Value:yyyy-MM-dd HH:mm} UTC"
                    : null),
            cancellationToken);
        return true;
    }

    public async Task<ReferralCodeVerificationResult> VerifyReferralCodeAsync(
        ReferralCodeVerificationViewModel model,
        Guid referredHospitalId,
        string? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        string code = NormalizeReferralVerificationCode(model.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            return ReferralCodeVerificationResult.Failure("Enter the referral verification code.");
        }

        IQueryable<Referral> query = _context.Referrals
            .Where(x =>
                !x.IsDeleted &&
                x.ReferredHospitalId == referredHospitalId &&
                x.ReferralVerificationCode == code);

        if (model.ReferralId.HasValue)
        {
            query = query.Where(x => x.Id == model.ReferralId.Value);
        }

        Referral? referral = await query.FirstOrDefaultAsync(cancellationToken);
        if (referral == null)
        {
            return ReferralCodeVerificationResult.Failure("The referral verification code is invalid for this referral facility.");
        }

        if (referral.Status == ReferralStatus.Closed)
        {
            return ReferralCodeVerificationResult.Failure("This referral has already been closed.");
        }

        if (referral.Status == ReferralStatus.Received && referral.ReferralVerificationCodeVerifiedAt.HasValue)
        {
            return ReferralCodeVerificationResult.Success(referral.Id, "Referral code was already verified.");
        }

        if (referral.Status != ReferralStatus.Verified && referral.Status != ReferralStatus.Audited)
        {
            return ReferralCodeVerificationResult.Failure("Only HMO-verified referrals can be verified by code.");
        }

        DateTime now = DateTime.UtcNow;
        if (!referral.ReferralVerificationCodeExpiresAt.HasValue ||
            referral.ReferralVerificationCodeExpiresAt.Value <= now)
        {
            return ReferralCodeVerificationResult.Failure("This referral verification code has expired. Ask the HMO to reactivate or issue a new code.");
        }

        referral.Status = ReferralStatus.Received;
        referral.ReferralVerificationCodeVerifiedAt = now;
        referral.ReferralVerificationCodeVerifiedByUserId = userId;
        referral.ReferralVerificationCodeVerifiedByName = userName;

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.ReferralCodeVerified,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = "Referral verification code confirmed by referred facility."
        });

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.Received,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = "Referral received after successful code verification."
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            "Referral.CodeVerified",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"ReferredHospital:{referral.ReferredHospitalId}",
                $"Status:{referral.Status}"),
            cancellationToken);
        return ReferralCodeVerificationResult.Success(referral.Id, "Referral code verified. Referral details are now available.");
    }

    public async Task<bool> AuditReferralAsync(ReferralAuditViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default)
    {
        Referral? referral = await _context.Referrals.FirstOrDefaultAsync(x => x.Id == model.ReferralId && !x.IsDeleted, cancellationToken);

        if (referral == null || referral.Status != ReferralStatus.Verified)
        {
            return false;
        }

        referral.Status = ReferralStatus.Audited;
        referral.AuditedByUserId = userId;
        referral.AuditedByName = userName;
        referral.AuditedAt = DateTime.UtcNow;
        referral.AuditNote = model.AuditNote.Trim();

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.Audited,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = model.AuditNote.Trim()
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(
            "Referral.Audited",
            AuditActor.Format(userName),
            referral.Id.ToString(),
            AuditActor.Details(
                $"Enrollee:{referral.EnrolleeNumber}",
                $"FromProvider:{referral.FromProviderName}",
                $"HMO:{referral.HmoName}",
                $"Status:{referral.Status}",
                $"Note:{model.AuditNote}"),
            cancellationToken);
        return true;
    }

    public async Task<EncounterReferralInputViewModel> BuildEncounterReferralInputAsync(EncounterReferralInputViewModel? model = null, CancellationToken cancellationToken = default)
    {
        EncounterReferralInputViewModel viewModel = model ?? new EncounterReferralInputViewModel();
        viewModel.ReferredHospitals = await GetHospitalSelectListAsync(viewModel.ReferredHospitalId, cancellationToken);
        return viewModel;
    }

    public async Task<Guid?> CreateReferralFromEncounterAsync(
        Guid encounterId,
        string? encounterReference,
        Guid? enrolleeId,
        string enrolleeNumber,
        string enrolleeFullName,
        string? hmoCode,
        string? hmoName,
        string? fromProviderId,
        string fromProviderName,
        EncounterReferralInputViewModel model,
        string? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        if (!model.RequiresReferral)
        {
            return null;
        }

        if (!model.ReferredHospitalId.HasValue)
        {
            throw new InvalidOperationException("A referred hospital must be selected before creating a referral.");
        }

        if (string.IsNullOrWhiteSpace(model.Diagnosis))
        {
            throw new InvalidOperationException("Diagnosis is required before creating a referral.");
        }

        if (string.IsNullOrWhiteSpace(model.ReasonForReferral))
        {
            throw new InvalidOperationException("Reason for referral is required before creating a referral.");
        }

        ReferralCreateViewModel createModel = new ReferralCreateViewModel
        {
            EncounterId = encounterId,
            EncounterReference = encounterReference,
            EnrolleeId = enrolleeId,
            EnrolleeNumber = enrolleeNumber,
            EnrolleeFullName = enrolleeFullName,
            HmoCode = hmoCode,
            HmoName = hmoName,
            FromProviderId = fromProviderId,
            FromProviderName = fromProviderName,
            ReferredHospitalId = model.ReferredHospitalId.Value,
            Diagnosis = model.Diagnosis,
            ReasonForReferral = model.ReasonForReferral,
            ClinicalSummary = model.ClinicalSummary,
            TreatmentGiven = model.TreatmentGiven,
            InvestigationSummary = model.InvestigationSummary,
            Priority = model.Priority
        };

        return await CreateReferralAsync(createModel, userId, userName, true, cancellationToken);
    }

    private async Task IssueReferralVerificationCodeAsync(
        Referral referral,
        string? userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        referral.ReferralVerificationCode = await GenerateUniqueReferralVerificationCodeAsync(cancellationToken);
        referral.ReferralVerificationCodeIssuedAt = now;
        referral.ReferralVerificationCodeExpiresAt = now.AddDays(ReferralVerificationCodeValidDays);
        referral.ReferralVerificationCodeIssuedByUserId = userId;
        referral.ReferralVerificationCodeIssuedByName = userName;
        referral.ReferralVerificationCodeVerifiedAt = null;
        referral.ReferralVerificationCodeVerifiedByUserId = null;
        referral.ReferralVerificationCodeVerifiedByName = null;

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = ReferralAuditAction.ReferralCodeIssued,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = $"Referral verification code issued. It expires on {referral.ReferralVerificationCodeExpiresAt.Value:yyyy-MM-dd HH:mm} UTC."
        });
    }

    private async Task<string> GenerateUniqueReferralVerificationCodeAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string code = "RVC" + RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
            bool exists = await _context.Referrals
                .AnyAsync(x => x.ReferralVerificationCode == code && !x.IsDeleted, cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        return "RVC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private static string NormalizeReferralVerificationCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return new string(code
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static void ClearReferralVerificationCode(Referral referral)
    {
        referral.ReferralVerificationCode = null;
        referral.ReferralVerificationCodeIssuedAt = null;
        referral.ReferralVerificationCodeExpiresAt = null;
        referral.ReferralVerificationCodeIssuedByUserId = null;
        referral.ReferralVerificationCodeIssuedByName = null;
        referral.ReferralVerificationCodeVerifiedAt = null;
        referral.ReferralVerificationCodeVerifiedByUserId = null;
        referral.ReferralVerificationCodeVerifiedByName = null;
    }

    private async Task<List<SelectListItem>> GetHospitalSelectListAsync(Guid? selectedId, CancellationToken cancellationToken)
    {
        return await _context.ReferralHospitals
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(x.State) ? x.Name : x.Name + " - " + x.State,
                Selected = selectedId.HasValue && selectedId.Value == x.Id
            })
            .ToListAsync(cancellationToken);
    }
}
