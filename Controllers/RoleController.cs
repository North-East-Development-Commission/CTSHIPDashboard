using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CTSHIPDashboard.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        // LIST ALL ROLES
        public IActionResult Index()
        {
            var roles = _roleManager.Roles.OrderBy(r => r.Name).ToList();
            return View(roles);
        }

        // CREATE GET
        public IActionResult Create()
        {
            return View();
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("", "Role name is required.");
                return View();
            }

            roleName = roleName.Trim();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError("", "Role already exists.");
                return View();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{roleName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // DETAILS
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            return View(role);
        }

        // DELETE GET
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            // Prevent deleting critical roles
            if (role.Name == "Admin" || role.Name == "SuperAdmin")
            {
                TempData["Error"] = "Cannot delete system critical roles.";
                return RedirectToAction(nameof(Index));
            }

            return View(role);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["Error"] = "Role not found.";
                return RedirectToAction(nameof(Index));
            }

            if (role.Name == "Admin")
            {
                TempData["Error"] = "Cannot delete Admin role.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Role '{role.Name}' deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to delete role.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
