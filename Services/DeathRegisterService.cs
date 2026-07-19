using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Services
{
    public class DeathRegisterService : IDeathRegisterService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public DeathRegisterService(
            ApplicationDbContext context,
            IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<DeathRegisterIndexViewModel>> GetProviderDeathRegistersAsync(
            string? providerId,
            string? providerCode,
            string? search,
            CancellationToken cancellationToken = default)
        {
            IQueryable<DeathRegister> query = _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(providerId)
                || !string.IsNullOrWhiteSpace(providerCode))
            {
                query = query.Where(x =>
                    x.ProviderId == providerId
                    || x.ProviderId == providerCode);
            }

            query = ApplySearch(query, search);

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DeathRegisterIndexViewModel
                {
                    Id = x.Id,
                    EnrolleeId = x.EnrolleeId,
                    EnrolleeNumber = x.EnrolleeNumber,
                    EnrolleeFullName = x.EnrolleeFullName,
                    HmoCode = x.HmoCode,
                    HmoName = x.HmoName,
                    ProviderName = x.ProviderName,
                    DateOfDeath = x.DateOfDeath,
                    CauseOfDeath = x.CauseOfDeath,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    SubmittedToHmoAt = x.SubmittedToHmoAt,
                    VerifiedAt = x.VerifiedAt,
                    AuditedAt = x.AuditedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<DeathRegisterIndexViewModel>> GetHmoDeathRegistersAsync(string? hmoCode, string? search, CancellationToken cancellationToken = default)
        {
            IQueryable<DeathRegister> query = _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status != DeathRegisterStatus.Draft && x.Status != DeathRegisterStatus.Cancelled);

            if (!string.IsNullOrWhiteSpace(hmoCode))
            {
                query = query.Where(x => x.HmoCode == hmoCode);
            }

            query = ApplySearch(query, search);

            return await query
                .OrderByDescending(x => x.SubmittedToHmoAt ?? x.CreatedAt)
                .Select(x => new DeathRegisterIndexViewModel
                {
                    Id = x.Id,
                    EnrolleeId = x.EnrolleeId,
                    EnrolleeNumber = x.EnrolleeNumber,
                    EnrolleeFullName = x.EnrolleeFullName,
                    HmoCode = x.HmoCode,
                    HmoName = x.HmoName,
                    ProviderName = x.ProviderName,
                    DateOfDeath = x.DateOfDeath,
                    CauseOfDeath = x.CauseOfDeath,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt,
                    SubmittedToHmoAt = x.SubmittedToHmoAt,
                    VerifiedAt = x.VerifiedAt,
                    AuditedAt = x.AuditedAt
                })
                .ToListAsync(cancellationToken);
        }

        public Task<DeathRegisterCreateViewModel> BuildCreateViewModelAsync(DeathRegisterCreateViewModel? model = null, CancellationToken cancellationToken = default)
        {
            DeathRegisterCreateViewModel viewModel = model ?? new DeathRegisterCreateViewModel();

            if (!viewModel.DateOfDeath.HasValue)
            {
                viewModel.DateOfDeath = DateTime.Today;
            }

            return Task.FromResult(viewModel);
        }

        public async Task<Guid> CreateDeathRegisterAsync(DeathRegisterCreateViewModel model, string? userId, string? userName, bool submitToHmo, CancellationToken cancellationToken = default)
        {
            if (!model.DateOfDeath.HasValue)
            {
                throw new InvalidOperationException("Date of death is required.");
            }

            DateTime now = DateTime.UtcNow;

            DeathRegister deathRegister = new DeathRegister
            {
                Id = Guid.NewGuid(),
                EnrolleeId = model.EnrolleeId,
                EnrolleeNumber = model.EnrolleeNumber.Trim(),
                EnrolleeFullName = model.EnrolleeFullName.Trim(),
                Gender = Normalize(model.Gender),
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = Normalize(model.PhoneNumber),
                Address = Normalize(model.Address),
                HmoCode = Normalize(model.HmoCode),
                HmoName = Normalize(model.HmoName),
                ProviderId = Normalize(model.ProviderId),
                ProviderName = model.ProviderName.Trim(),
                DateOfDeath = model.DateOfDeath.Value.Date,
                TimeOfDeath = model.TimeOfDeath,
                PlaceOfDeath = model.PlaceOfDeath.Trim(),
                CauseOfDeath = model.CauseOfDeath.Trim(),
                CauseCategory = model.CauseCategory,
                DeathConfirmedBy = model.DeathConfirmedBy.Trim(),
                DeathConfirmedByDesignation = Normalize(model.DeathConfirmedByDesignation),
                DeathConfirmedByPhone = Normalize(model.DeathConfirmedByPhone),
                DeathCertificateNumber = Normalize(model.DeathCertificateNumber),
                DeathCertificateFilePath = Normalize(model.DeathCertificateFilePath),
                ProviderRemarks = Normalize(model.ProviderRemarks),
                Status = submitToHmo ? DeathRegisterStatus.SubmittedToHmo : DeathRegisterStatus.Draft,
                CreatedByUserId = userId,
                CreatedByName = userName,
                CreatedAt = now,
                SubmittedToHmoAt = submitToHmo ? now : null,
                SubmittedByUserId = submitToHmo ? userId : null,
                SubmittedByName = submitToHmo ? userName : null
            };

            _context.DeathRegisters.Add(deathRegister);
            AddAuditLog(deathRegister, DeathRegisterAuditAction.Created, userId, userName, "Death register created by provider.");

            if (submitToHmo)
            {
                AddAuditLog(deathRegister, DeathRegisterAuditAction.SubmittedToHmo, userId, userName, "Death register submitted to HMO for verification.");
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(
                submitToHmo ? "DeathRegister.CreatedAndSubmitted" : "DeathRegister.Created",
                AuditActor.Format(userName),
                deathRegister.EnrolleeNumber,
                AuditActor.Details(
                    $"Name:{deathRegister.EnrolleeFullName}",
                    $"Provider:{deathRegister.ProviderName}",
                    $"HMO:{deathRegister.HmoName}",
                    $"Status:{deathRegister.Status}"),
                cancellationToken);
            return deathRegister.Id;
        }

        public async Task<DeathRegisterDetailsViewModel?> GetDeathRegisterDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .AsNoTracking()
                .Include(x => x.AuditLogs)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (deathRegister == null)
            {
                return null;
            }

            return MapDetails(deathRegister);
        }

        public async Task<bool> SubmitDeathRegisterToHmoAsync(Guid id, string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .Include(x => x.AuditLogs)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (deathRegister == null || deathRegister.Status != DeathRegisterStatus.Draft)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;
            deathRegister.Status = DeathRegisterStatus.SubmittedToHmo;
            deathRegister.SubmittedToHmoAt = now;
            deathRegister.SubmittedByUserId = userId;
            deathRegister.SubmittedByName = userName;
            AddAuditLog(deathRegister, DeathRegisterAuditAction.SubmittedToHmo, userId, userName, "Death register submitted to HMO for verification.");
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(
                "DeathRegister.Submitted",
                AuditActor.Format(userName),
                deathRegister.EnrolleeNumber,
                AuditActor.Details(
                    $"Name:{deathRegister.EnrolleeFullName}",
                    $"Provider:{deathRegister.ProviderName}",
                    $"HMO:{deathRegister.HmoName}",
                    $"Status:{deathRegister.Status}"),
                cancellationToken);
            return true;
        }

        public async Task<DeathRegisterVerificationViewModel?> BuildVerificationViewModelAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id
                        && !x.IsDeleted
                        && x.Status == DeathRegisterStatus.SubmittedToHmo,
                    cancellationToken);

            if (deathRegister == null)
            {
                return null;
            }

            return new DeathRegisterVerificationViewModel
            {
                Id = deathRegister.Id,
                EnrolleeNumber = deathRegister.EnrolleeNumber,
                EnrolleeFullName = deathRegister.EnrolleeFullName,
                ProviderName = deathRegister.ProviderName,
                DateOfDeath = deathRegister.DateOfDeath,
                CauseOfDeath = deathRegister.CauseOfDeath,
                IsVerified = true,
                HmoVerificationNote = deathRegister.HmoVerificationNote
            };
        }

        public async Task<bool> VerifyDeathRegisterAsync(DeathRegisterVerificationViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .Include(x => x.AuditLogs)
                .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

            if (deathRegister == null || deathRegister.Status != DeathRegisterStatus.SubmittedToHmo)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;
            bool isVerified = model.IsVerified == true;
            deathRegister.Status = isVerified ? DeathRegisterStatus.HmoVerified : DeathRegisterStatus.HmoRejected;
            deathRegister.VerifiedByUserId = userId;
            deathRegister.VerifiedByName = userName;
            deathRegister.VerifiedAt = now;
            deathRegister.HmoVerificationNote = Normalize(model.HmoVerificationNote);

            AddAuditLog(
                deathRegister,
                isVerified ? DeathRegisterAuditAction.HmoVerified : DeathRegisterAuditAction.HmoRejected,
                userId,
                userName,
                model.HmoVerificationNote);

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(
                isVerified ? "DeathRegister.HmoVerified" : "DeathRegister.HmoRejected",
                AuditActor.Format(userName),
                deathRegister.EnrolleeNumber,
                AuditActor.Details(
                    $"Name:{deathRegister.EnrolleeFullName}",
                    $"Provider:{deathRegister.ProviderName}",
                    $"HMO:{deathRegister.HmoName}",
                    $"Status:{deathRegister.Status}",
                    $"Note:{model.HmoVerificationNote}"),
                cancellationToken);
            return true;
        }

        public async Task<DeathRegisterAuditViewModel?> BuildAuditViewModelAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id
                        && !x.IsDeleted
                        && x.Status == DeathRegisterStatus.HmoVerified,
                    cancellationToken);

            if (deathRegister == null)
            {
                return null;
            }

            return new DeathRegisterAuditViewModel
            {
                Id = deathRegister.Id,
                EnrolleeNumber = deathRegister.EnrolleeNumber,
                EnrolleeFullName = deathRegister.EnrolleeFullName,
                ProviderName = deathRegister.ProviderName,
                DateOfDeath = deathRegister.DateOfDeath,
                CauseOfDeath = deathRegister.CauseOfDeath,
                HmoVerificationNote = deathRegister.HmoVerificationNote,
                IsApproved = true,
                AuditNote = deathRegister.AuditNote
            };
        }

        public async Task<bool> AuditDeathRegisterAsync(DeathRegisterAuditViewModel model, string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            DeathRegister? deathRegister = await _context.DeathRegisters
                .Include(x => x.AuditLogs)
                .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

            if (deathRegister == null || deathRegister.Status != DeathRegisterStatus.HmoVerified)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;
            bool isApproved = model.IsApproved == true;
            deathRegister.Status = isApproved ? DeathRegisterStatus.Audited : DeathRegisterStatus.AuditRejected;
            deathRegister.AuditedByUserId = userId;
            deathRegister.AuditedByName = userName;
            deathRegister.AuditedAt = now;
            deathRegister.AuditNote = Normalize(model.AuditNote);

            AddAuditLog(
                deathRegister,
                isApproved ? DeathRegisterAuditAction.Audited : DeathRegisterAuditAction.AuditRejected,
                userId,
                userName,
                model.AuditNote);

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(
                isApproved ? "DeathRegister.Audited" : "DeathRegister.AuditRejected",
                AuditActor.Format(userName),
                deathRegister.EnrolleeNumber,
                AuditActor.Details(
                    $"Name:{deathRegister.EnrolleeFullName}",
                    $"Provider:{deathRegister.ProviderName}",
                    $"HMO:{deathRegister.HmoName}",
                    $"Status:{deathRegister.Status}",
                    $"Note:{model.AuditNote}"),
                cancellationToken);
            return true;
        }

        public async Task<Dictionary<int, EnrolleeDeathStatusViewModel>> GetDeathStatusMapAsync(IEnumerable<int> enrolleeIds, CancellationToken cancellationToken = default)
        {
            List<int> ids = enrolleeIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, EnrolleeDeathStatusViewModel>();
            }

            List<DeathRegister> records = await _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.EnrolleeId.HasValue
                    && ids.Contains(x.EnrolleeId.Value)
                    && x.Status != DeathRegisterStatus.Cancelled
                    && x.Status != DeathRegisterStatus.HmoRejected
                    && x.Status != DeathRegisterStatus.AuditRejected)
                .OrderByDescending(x => x.DateOfDeath)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return records
                .GroupBy(x => x.EnrolleeId!.Value)
                .ToDictionary(x => x.Key, x => ToEnrolleeDeathStatus(x.First()));
        }

        public async Task<Dictionary<string, EnrolleeDeathStatusViewModel>> GetDeathStatusMapByEnrolleeNumberAsync(IEnumerable<string> enrolleeNumbers, CancellationToken cancellationToken = default)
        {
            List<string> numbers = enrolleeNumbers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (numbers.Count == 0)
            {
                return new Dictionary<string, EnrolleeDeathStatusViewModel>(StringComparer.OrdinalIgnoreCase);
            }

            List<DeathRegister> records = await _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && numbers.Contains(x.EnrolleeNumber)
                    && x.Status != DeathRegisterStatus.Cancelled
                    && x.Status != DeathRegisterStatus.HmoRejected
                    && x.Status != DeathRegisterStatus.AuditRejected)
                .OrderByDescending(x => x.DateOfDeath)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return records
                .GroupBy(x => x.EnrolleeNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => ToEnrolleeDeathStatus(x.First()), StringComparer.OrdinalIgnoreCase);
        }

        public async Task<EnrolleeDeathStatusViewModel> GetEnrolleeDeathStatusAsync(int? enrolleeId, string? enrolleeNumber, CancellationToken cancellationToken = default)
        {
            IQueryable<DeathRegister> query = _context.DeathRegisters
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.Status != DeathRegisterStatus.Cancelled
                    && x.Status != DeathRegisterStatus.HmoRejected
                    && x.Status != DeathRegisterStatus.AuditRejected);

            if (enrolleeId.HasValue && enrolleeId.Value > 0)
            {
                query = query.Where(x => x.EnrolleeId == enrolleeId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(enrolleeNumber))
            {
                string normalizedNumber = enrolleeNumber.Trim();
                query = query.Where(x => x.EnrolleeNumber == normalizedNumber);
            }
            else
            {
                return EnrolleeDeathStatusViewModel.Active();
            }

            DeathRegister? record = await query
                .OrderByDescending(x => x.DateOfDeath)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return record == null ? EnrolleeDeathStatusViewModel.Active() : ToEnrolleeDeathStatus(record);
        }

        private static IQueryable<DeathRegister> ApplySearch(IQueryable<DeathRegister> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return query;
            }

            string keyword = search.Trim();

            return query.Where(x => x.EnrolleeNumber.Contains(keyword)
                || x.EnrolleeFullName.Contains(keyword)
                || x.ProviderName.Contains(keyword)
                || (x.HmoName != null && x.HmoName.Contains(keyword))
                || (x.HmoCode != null && x.HmoCode.Contains(keyword))
                || x.CauseOfDeath.Contains(keyword));
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static void AddAuditLog(DeathRegister deathRegister, DeathRegisterAuditAction action, string? userId, string? userName, string? note)
        {
            deathRegister.AuditLogs.Add(new DeathRegisterAuditLog
            {
                Id = Guid.NewGuid(),
                DeathRegisterId = deathRegister.Id,
                Action = action,
                ActionByUserId = userId,
                ActionByName = userName,
                ActionAt = DateTime.UtcNow,
                Note = Normalize(note)
            });
        }

        private static EnrolleeDeathStatusViewModel ToEnrolleeDeathStatus(DeathRegister deathRegister)
        {
            return new EnrolleeDeathStatusViewModel
            {
                IsDeceased = true,
                RegisterStatus = deathRegister.Status,
                DeathRegisterId = deathRegister.Id,
                DateOfDeath = deathRegister.DateOfDeath
            };
        }

        private static DeathRegisterDetailsViewModel MapDetails(DeathRegister deathRegister)
        {
            return new DeathRegisterDetailsViewModel
            {
                Id = deathRegister.Id,
                EnrolleeId = deathRegister.EnrolleeId,
                EnrolleeNumber = deathRegister.EnrolleeNumber,
                EnrolleeFullName = deathRegister.EnrolleeFullName,
                Gender = deathRegister.Gender,
                DateOfBirth = deathRegister.DateOfBirth,
                PhoneNumber = deathRegister.PhoneNumber,
                Address = deathRegister.Address,
                HmoCode = deathRegister.HmoCode,
                HmoName = deathRegister.HmoName,
                ProviderId = deathRegister.ProviderId,
                ProviderName = deathRegister.ProviderName,
                DateOfDeath = deathRegister.DateOfDeath,
                TimeOfDeath = deathRegister.TimeOfDeath,
                PlaceOfDeath = deathRegister.PlaceOfDeath,
                CauseOfDeath = deathRegister.CauseOfDeath,
                CauseCategory = deathRegister.CauseCategory,
                DeathConfirmedBy = deathRegister.DeathConfirmedBy,
                DeathConfirmedByDesignation = deathRegister.DeathConfirmedByDesignation,
                DeathConfirmedByPhone = deathRegister.DeathConfirmedByPhone,
                DeathCertificateNumber = deathRegister.DeathCertificateNumber,
                DeathCertificateFilePath = deathRegister.DeathCertificateFilePath,
                ProviderRemarks = deathRegister.ProviderRemarks,
                Status = deathRegister.Status,
                CreatedByName = deathRegister.CreatedByName,
                CreatedAt = deathRegister.CreatedAt,
                SubmittedByName = deathRegister.SubmittedByName,
                SubmittedToHmoAt = deathRegister.SubmittedToHmoAt,
                VerifiedByName = deathRegister.VerifiedByName,
                VerifiedAt = deathRegister.VerifiedAt,
                HmoVerificationNote = deathRegister.HmoVerificationNote,
                AuditedByName = deathRegister.AuditedByName,
                AuditedAt = deathRegister.AuditedAt,
                AuditNote = deathRegister.AuditNote,
                AuditLogs = deathRegister.AuditLogs
                    .OrderByDescending(x => x.ActionAt)
                    .Select(x => new DeathRegisterAuditLogViewModel
                    {
                        Action = x.Action,
                        ActionByName = x.ActionByName,
                        ActionAt = x.ActionAt,
                        Note = x.Note
                    })
                    .ToList()
            };
        }
    }

}
