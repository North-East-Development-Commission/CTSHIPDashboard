using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.Enums;
using CTSHIPDashboard.Models.ViewModels;
using CTSHIPDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "CTSHIPAdmin,HMO,Provider,StateOffice,Monitoring,NHIA,NEDCAdmin,SSHIA")]
    public class ComplaintsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ComplaintsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(
            string search = "",
            ComplaintStatus? status = null,
            ComplaintPriority? priority = null,
            int page = 1,
            int pageSize = 20)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            IQueryable<Complaint> query = ApplyScope(
                _context.Complaints
                    .AsNoTracking()
                    .Include(complaint => complaint.Hmo)
                    .Include(complaint => complaint.Provider)
                    .Include(complaint => complaint.Enrollee),
                user);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = $"%{search.Trim()}%";
                query = query.Where(complaint =>
                    EF.Functions.Like(complaint.ReferenceNumber, term)
                    || EF.Functions.Like(complaint.Subject, term)
                    || EF.Functions.Like(complaint.Description, term)
                    || (complaint.SubmittedByName != null
                        && EF.Functions.Like(complaint.SubmittedByName, term)));
            }

            if (status.HasValue)
            {
                query = query.Where(complaint => complaint.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(complaint => complaint.Priority == priority.Value);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            int totalItems = await query.CountAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;
            ViewBag.CanManage = CanManageComplaints();

            return View(await query
                .OrderByDescending(complaint => complaint.Priority)
                .ThenByDescending(complaint => complaint.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            ComplaintCreateViewModel model = new();
            await PopulateCreateModelAsync(model, user);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplaintCreateViewModel model)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            await ApplyAndValidateScopeAsync(model, user);

            if (!ModelState.IsValid)
            {
                await PopulateCreateModelAsync(model, user);
                return View(model);
            }

            IList<string> roles = await _userManager.GetRolesAsync(user);
            Complaint complaint = new()
            {
                ReferenceNumber = await GenerateReferenceAsync(),
                Subject = model.Subject.Trim(),
                Description = model.Description.Trim(),
                Category = model.Category,
                Priority = model.Priority,
                Status = ComplaintStatus.Open,
                State = model.State.Trim(),
                HmoId = model.HmoId,
                ProviderId = model.ProviderId,
                EnrolleeId = model.EnrolleeId,
                SubmittedByUserId = user.Id,
                SubmittedByName = user.FullName ?? user.UserName ?? user.Email,
                SubmittedByRole = string.Join(", ", roles),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
                "Complaint.Created",
                AuditActor.Format(user, User.Identity?.Name),
                complaint.ReferenceNumber,
                AuditActor.Details(
                    $"Category:{complaint.Category}",
                    $"Priority:{complaint.Priority}",
                    $"State:{complaint.State}",
                    $"HMO:{complaint.HmoId}",
                    $"Provider:{complaint.ProviderId}"),
                HttpContext.RequestAborted);

            TempData["Success"] = $"Complaint {complaint.ReferenceNumber} was submitted successfully.";
            return RedirectToAction(nameof(Details), new { id = complaint.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            Complaint? complaint = await ApplyScope(
                    _context.Complaints
                        .AsNoTracking()
                        .Include(item => item.Hmo)
                        .Include(item => item.Provider)
                        .Include(item => item.Enrollee),
                    user)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (complaint == null) return NotFound();
            ViewBag.CanManage = CanManageComplaints();
            return View(complaint);
        }

        [Authorize(Roles = "CTSHIPAdmin,HMO,StateOffice,Monitoring,NHIA,NEDCAdmin,SSHIA")]
        public async Task<IActionResult> Manage(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            Complaint? complaint = await ApplyScope(_context.Complaints.AsNoTracking(), user)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (complaint == null) return NotFound();

            return View(new ComplaintUpdateViewModel
            {
                Id = complaint.Id,
                ReferenceNumber = complaint.ReferenceNumber,
                Subject = complaint.Subject,
                Status = complaint.Status,
                Priority = complaint.Priority,
                AssignedToName = complaint.AssignedToName,
                ResolutionNote = complaint.ResolutionNote
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CTSHIPAdmin,HMO,StateOffice,Monitoring,NHIA,NEDCAdmin,SSHIA")]
        public async Task<IActionResult> Manage(int id, ComplaintUpdateViewModel model)
        {
            if (id != model.Id) return BadRequest();

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            Complaint? complaint = await ApplyScope(_context.Complaints, user)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (complaint == null) return NotFound();

            if ((model.Status == ComplaintStatus.Resolved
                    || model.Status == ComplaintStatus.Closed
                    || model.Status == ComplaintStatus.Rejected)
                && string.IsNullOrWhiteSpace(model.ResolutionNote))
            {
                ModelState.AddModelError(
                    nameof(model.ResolutionNote),
                    "Enter a resolution or closure note for this status.");
            }

            if (!ModelState.IsValid)
            {
                model.ReferenceNumber = complaint.ReferenceNumber;
                model.Subject = complaint.Subject;
                return View(model);
            }

            complaint.Status = model.Status;
            complaint.Priority = model.Priority;
            complaint.AssignedToName = Normalize(model.AssignedToName);
            complaint.ResolutionNote = Normalize(model.ResolutionNote);
            complaint.UpdatedAt = DateTime.UtcNow;
            complaint.ResolvedAt =
                model.Status is ComplaintStatus.Resolved or ComplaintStatus.Closed
                    ? DateTime.UtcNow
                    : null;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
                "Complaint.Updated",
                AuditActor.Format(user, User.Identity?.Name),
                complaint.ReferenceNumber,
                AuditActor.Details(
                    $"Status:{complaint.Status}",
                    $"Priority:{complaint.Priority}",
                    $"AssignedTo:{complaint.AssignedToName}",
                    $"Resolution:{complaint.ResolutionNote}"),
                HttpContext.RequestAborted);
            TempData["Success"] = $"Complaint {complaint.ReferenceNumber} was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private IQueryable<Complaint> ApplyScope(
            IQueryable<Complaint> query,
            ApplicationUser user)
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Monitoring")
                || User.IsInRole("NHIA")
                || User.IsInRole("NEDCAdmin"))
            {
                return query;
            }

            if (User.IsInRole("HMO"))
            {
                return user.HmoId.HasValue
                    ? query.Where(complaint => complaint.HmoId == user.HmoId.Value)
                    : query.Where(_ => false);
            }

            if (User.IsInRole("Provider"))
            {
                return user.ProviderId.HasValue
                    ? query.Where(complaint => complaint.ProviderId == user.ProviderId.Value)
                    : query.Where(_ => false);
            }

            if (User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
            {
                return !string.IsNullOrWhiteSpace(user.State)
                    ? query.Where(complaint => complaint.State == user.State)
                    : query.Where(_ => false);
            }

            return query.Where(_ => false);
        }

        private async Task ApplyAndValidateScopeAsync(
            ComplaintCreateViewModel model,
            ApplicationUser user)
        {
            if (model.EnrolleeId.HasValue)
            {
                Enrollee? enrollee = await _context.Enrollees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == model.EnrolleeId.Value);
                if (enrollee == null || !CanUseEnrollee(enrollee, user))
                {
                    ModelState.AddModelError(nameof(model.EnrolleeId), "Select an enrollee within your assigned scope.");
                }
                else
                {
                    model.State = enrollee.State;
                    model.HmoId = enrollee.HmoId;
                    model.ProviderId = enrollee.ProviderId;
                }
            }

            if (User.IsInRole("Provider"))
            {
                Provider? provider = user.ProviderId.HasValue
                    ? await _context.Providers.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.Id == user.ProviderId.Value)
                    : null;
                if (provider == null)
                {
                    ModelState.AddModelError(string.Empty, "Your account is not linked to a provider.");
                    return;
                }

                model.ProviderId = provider.Id;
                model.HmoId = provider.HmoId;
                model.State = provider.State;
            }
            else if (User.IsInRole("HMO"))
            {
                Hmo? hmo = user.HmoId.HasValue
                    ? await _context.Hmos.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.Id == user.HmoId.Value)
                    : null;
                if (hmo == null)
                {
                    ModelState.AddModelError(string.Empty, "Your account is not linked to an HMO.");
                    return;
                }

                model.HmoId = hmo.Id;
                model.State = hmo.State;
                if (model.ProviderId.HasValue
                    && !await _context.Providers.AnyAsync(provider =>
                        provider.Id == model.ProviderId.Value && provider.HmoId == hmo.Id))
                {
                    ModelState.AddModelError(nameof(model.ProviderId), "Select a provider under your HMO.");
                }
            }
            else if (User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
            {
                model.State = user.State?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(model.State))
                {
                    ModelState.AddModelError(nameof(model.State), "Your account has no assigned state.");
                }
            }

            if (model.ProviderId.HasValue)
            {
                Provider? provider = await _context.Providers.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == model.ProviderId.Value);
                if (provider == null
                    || (!string.IsNullOrWhiteSpace(model.State) && provider.State != model.State)
                    || (model.HmoId.HasValue && provider.HmoId != model.HmoId.Value))
                {
                    ModelState.AddModelError(nameof(model.ProviderId), "The selected provider is outside the complaint scope.");
                }
            }

            if (model.HmoId.HasValue)
            {
                Hmo? hmo = await _context.Hmos.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == model.HmoId.Value);
                if (hmo == null
                    || (!string.IsNullOrWhiteSpace(model.State) && hmo.State != model.State))
                {
                    ModelState.AddModelError(nameof(model.HmoId), "The selected HMO is outside the complaint state.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.State))
            {
                ModelState.AddModelError(nameof(model.State), "Select a state.");
            }
        }

        private bool CanUseEnrollee(Enrollee enrollee, ApplicationUser user)
        {
            if (User.IsInRole("CTSHIPAdmin")
                || User.IsInRole("Monitoring")
                || User.IsInRole("NHIA")
                || User.IsInRole("NEDCAdmin"))
            {
                return true;
            }

            if (User.IsInRole("Provider")) return enrollee.ProviderId == user.ProviderId;
            if (User.IsInRole("HMO")) return enrollee.HmoId == user.HmoId;
            return enrollee.State == user.State;
        }

        private async Task PopulateCreateModelAsync(
            ComplaintCreateViewModel model,
            ApplicationUser user)
        {
            IQueryable<Hmo> hmos = _context.Hmos.AsNoTracking();
            IQueryable<Provider> providers = _context.Providers.AsNoTracking();
            IQueryable<Enrollee> enrollees = _context.Enrollees.AsNoTracking();

            if (User.IsInRole("Provider") && user.ProviderId.HasValue)
            {
                Provider? provider = await providers.FirstOrDefaultAsync(item => item.Id == user.ProviderId.Value);
                if (provider != null)
                {
                    model.ProviderId = provider.Id;
                    model.HmoId = provider.HmoId;
                    model.State = provider.State;
                    model.ProviderLocked = true;
                    model.HmoLocked = true;
                    model.StateLocked = true;
                    providers = providers.Where(item => item.Id == provider.Id);
                    hmos = hmos.Where(item => item.Id == provider.HmoId);
                    enrollees = enrollees.Where(item => item.ProviderId == provider.Id);
                }
            }
            else if (User.IsInRole("HMO") && user.HmoId.HasValue)
            {
                Hmo? hmo = await hmos.FirstOrDefaultAsync(item => item.Id == user.HmoId.Value);
                if (hmo != null)
                {
                    model.HmoId = hmo.Id;
                    model.State = hmo.State;
                    model.HmoLocked = true;
                    model.StateLocked = true;
                    hmos = hmos.Where(item => item.Id == hmo.Id);
                    providers = providers.Where(item => item.HmoId == hmo.Id);
                    enrollees = enrollees.Where(item => item.HmoId == hmo.Id);
                }
            }
            else if ((User.IsInRole("StateOffice") || User.IsInRole("SSHIA"))
                && !string.IsNullOrWhiteSpace(user.State))
            {
                model.State = user.State;
                model.StateLocked = true;
                hmos = hmos.Where(item => item.State == user.State);
                providers = providers.Where(item => item.State == user.State);
                enrollees = enrollees.Where(item => item.State == user.State);
            }

            model.States = StateSelectListHelper.NorthEastStates(model.State);
            model.Hmos = await hmos.OrderBy(item => item.Name)
                .Select(item => new SelectListItem(item.Name, item.Id.ToString(), item.Id == model.HmoId))
                .ToListAsync();
            model.Providers = await providers.OrderBy(item => item.Name)
                .Select(item => new SelectListItem(item.Name, item.Id.ToString(), item.Id == model.ProviderId))
                .ToListAsync();
            model.Enrollees = await enrollees.OrderBy(item => item.FullName)
                .Take(500)
                .Select(item => new SelectListItem(
                    item.FullName + " - " + item.EnrollmentNumber,
                    item.Id.ToString(),
                    item.Id == model.EnrolleeId))
                .ToListAsync();
        }

        private bool CanManageComplaints() =>
            User.IsInRole("CTSHIPAdmin")
            || User.IsInRole("HMO")
            || User.IsInRole("StateOffice")
            || User.IsInRole("Monitoring")
            || User.IsInRole("NHIA")
            || User.IsInRole("NEDCAdmin")
            || User.IsInRole("SSHIA");

        private async Task<string> GenerateReferenceAsync()
        {
            string reference;
            do
            {
                reference = $"CMP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
            }
            while (await _context.Complaints.AnyAsync(item => item.ReferenceNumber == reference));

            return reference;
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
