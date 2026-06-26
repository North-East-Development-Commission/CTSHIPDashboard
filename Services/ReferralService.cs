using CTSHIPDashboard.Models;
using CTSHIPDashboard.Enums;
using CTSHIPDashboard.ViewModels;
using CTSHIPDashboard.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services;

public class ReferralService : IReferralService
{
    private readonly ApplicationDbContext _context;

    public ReferralService(ApplicationDbContext context)
    {
        _context = context;
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
                AuditedAt = x.AuditedAt
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
                AuditedAt = x.AuditedAt
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

        _context.ReferralAuditLogs.Add(new ReferralAuditLog
        {
            ReferralId = referral.Id,
            Action = model.IsApproved ? ReferralAuditAction.Verified : ReferralAuditAction.Rejected,
            PerformedByUserId = userId,
            PerformedByName = userName,
            Note = model.VerificationNote.Trim()
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
