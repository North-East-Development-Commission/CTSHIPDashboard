using CTSHIPDashboard.Data;
using CTSHIPDashboard.Models;
using CTSHIPDashboard.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class FeedbackController : Controller
{
    private readonly ApplicationDbContext _context;

    public FeedbackController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var feedbacks = await _context.Feedbacks
            .Include(f => f.Enrollee)
            .Include(f => f.Provider)
            .ToListAsync();

        var model = new FeedbackViewModel
        {
            Feedbacks = feedbacks,
            AverageSatisfaction = feedbacks.Any() ? feedbacks.Average(f => f.SatisfactionScore) : 0,
            ResolutionIndex = feedbacks.Any() ? Math.Round(feedbacks.Count(f => f.Resolved) * 100.0 / feedbacks.Count, 2) : 0,
            ScoresDistribution = feedbacks.GroupBy(f => f.SatisfactionScore)
                                          .ToDictionary(g => g.Key, g => g.Count())
        };

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Enrollees = await _context.Enrollees.ToListAsync();
        ViewBag.Providers = await _context.Providers.ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Feedback feedback)
    {
        if (ModelState.IsValid)
        {
            _context.Add(feedback);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Enrollees = await _context.Enrollees.ToListAsync();
        ViewBag.Providers = await _context.Providers.ToListAsync();
        return View(feedback);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null) return NotFound();
        ViewBag.Enrollees = await _context.Enrollees.ToListAsync();
        ViewBag.Providers = await _context.Providers.ToListAsync();
        return View(feedback);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Feedback feedback)
    {
        if (id != feedback.Id) return NotFound();
        if (ModelState.IsValid)
        {
            _context.Update(feedback);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Enrollees = await _context.Enrollees.ToListAsync();
        ViewBag.Providers = await _context.Providers.ToListAsync();
        return View(feedback);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var feedback = await _context.Feedbacks
            .Include(f => f.Enrollee)
            .Include(f => f.Provider)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (feedback == null) return NotFound();
        return View(feedback);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback != null)
        {
            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}