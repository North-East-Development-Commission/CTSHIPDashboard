using CTSHIPDashboard.Data;
using CTSHIPDashboard.Helpers;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Controllers;

[Authorize(Roles = "Admin,NHIA,HMO")]
public class ReferralHospitalsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReferralHospitalsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? state,
        string status = "All",
        CancellationToken cancellationToken = default)
    {
        IQueryable<ReferredHospital> query = _context.ReferralHospitals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) ||
                                     (x.State != null && x.State.Contains(term)) ||
                                     (x.Lga != null && x.Lga.Contains(term)) ||
                                     (x.ContactPerson != null && x.ContactPerson.Contains(term)) ||
                                     (x.PhoneNumber != null && x.PhoneNumber.Contains(term)));
        }

        if (NorthEastLocationData.IsValidState(state))
        {
            string selectedState = state!.Trim();
            query = query.Where(x => x.State == selectedState);
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        List<ReferralHospitalViewModel> hospitals = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.State)
            .ThenBy(x => x.Name)
            .Select(x => new ReferralHospitalViewModel
            {
                Id = x.Id,
                Name = x.Name,
                State = x.State,
                Lga = x.Lga,
                Address = x.Address,
                ContactPerson = x.ContactPerson,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        PopulateIndexFilters(state, status);
        ViewBag.Search = search;
        return View(hospitals);
    }

    public IActionResult Create()
    {
        ReferralHospitalViewModel model = new();
        PopulateLocationLists(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ReferralHospitalViewModel model,
        CancellationToken cancellationToken)
    {
        await ValidateHospitalModelAsync(model, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            PopulateLocationLists(model);
            return View(model);
        }

        ReferredHospital hospital = new()
        {
            Name = model.Name.Trim(),
            State = model.State!.Trim(),
            Lga = model.Lga!.Trim(),
            Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(model.ContactPerson) ? null : model.ContactPerson.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReferralHospitals.Add(hospital);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Referral hospital created and added to the provider referral dropdown.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        ReferredHospital? hospital =
            await _context.ReferralHospitals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (hospital == null)
        {
            return NotFound();
        }

        ReferralHospitalViewModel model = MapToViewModel(hospital);
        PopulateLocationLists(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        ReferralHospitalViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        await ValidateHospitalModelAsync(model, id, cancellationToken);

        if (!ModelState.IsValid)
        {
            PopulateLocationLists(model);
            return View(model);
        }

        ReferredHospital? hospital =
            await _context.ReferralHospitals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (hospital == null)
        {
            return NotFound();
        }

        hospital.Name = model.Name.Trim();
        hospital.State = model.State!.Trim();
        hospital.Lga = model.Lga!.Trim();
        hospital.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        hospital.ContactPerson = string.IsNullOrWhiteSpace(model.ContactPerson) ? null : model.ContactPerson.Trim();
        hospital.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        hospital.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        hospital.IsActive = model.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Referral hospital updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        ReferredHospital? hospital =
            await _context.ReferralHospitals
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (hospital == null)
        {
            return NotFound();
        }

        ViewBag.ReferralCount = await _context.Referrals
            .CountAsync(x => x.ReferredHospitalId == id, cancellationToken);

        return View(MapToViewModel(hospital));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        ReferredHospital? hospital =
            await _context.ReferralHospitals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (hospital == null)
        {
            return NotFound();
        }

        int referralCount = await _context.Referrals
            .CountAsync(x => x.ReferredHospitalId == id, cancellationToken);

        if (referralCount > 0)
        {
            hospital.IsActive = false;
            TempData["SuccessMessage"] =
                "Referral hospital has existing referrals, so it was deactivated instead of deleted.";
        }
        else
        {
            _context.ReferralHospitals.Remove(hospital);
            TempData["SuccessMessage"] = "Referral hospital deleted.";
        }

        await _context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult GetLgasByState(string state)
    {
        if (!NorthEastLocationData.IsValidState(state))
        {
            return Json(Array.Empty<string>());
        }

        return Json(NorthEastLocationData.GetLgas(state));
    }

    private async Task ValidateHospitalModelAsync(
        ReferralHospitalViewModel model,
        Guid? existingId,
        CancellationToken cancellationToken)
    {
        if (!NorthEastLocationData.IsValidState(model.State))
        {
            ModelState.AddModelError(nameof(model.State), "Select one of the six North-East states.");
            return;
        }

        if (!NorthEastLocationData.IsValidLga(model.State, model.Lga))
        {
            ModelState.AddModelError(nameof(model.Lga), "Select an LGA that belongs to the selected state.");
            return;
        }

        string name = model.Name.Trim();
        string state = model.State!.Trim();
        bool duplicateExists = await _context.ReferralHospitals
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != existingId.GetValueOrDefault()
                && x.Name == name
                && x.State == state,
                cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError(nameof(model.Name), "A referral hospital with this name already exists in the selected state.");
        }
    }

    private void PopulateLocationLists(ReferralHospitalViewModel model)
    {
        ViewBag.States = NorthEastLocationData.States
            .Select(state => new SelectListItem(state, state, state == model.State))
            .ToList();

        ViewBag.Lgas = NorthEastLocationData.GetLgas(model.State)
            .Select(lga => new SelectListItem(lga, lga, lga == model.Lga))
            .ToList();
    }

    private void PopulateIndexFilters(string? state, string status)
    {
        ViewBag.States = NorthEastLocationData.States
            .Select(item => new SelectListItem(item, item, item == state))
            .ToList();

        ViewBag.Statuses = new List<SelectListItem>
        {
            new("All", "All", string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)),
            new("Active", "Active", string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)),
            new("Inactive", "Inactive", string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        };

        ViewBag.SelectedState = state;
        ViewBag.SelectedStatus = status;
    }

    private static ReferralHospitalViewModel MapToViewModel(ReferredHospital hospital) =>
        new()
        {
            Id = hospital.Id,
            Name = hospital.Name,
            State = hospital.State,
            Lga = hospital.Lga,
            Address = hospital.Address,
            ContactPerson = hospital.ContactPerson,
            PhoneNumber = hospital.PhoneNumber,
            Email = hospital.Email,
            IsActive = hospital.IsActive
        };
}
