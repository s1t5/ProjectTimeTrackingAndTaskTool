using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;

namespace ProjektZeiterfassung.Controllers
{
    public class KundeController : Controller
    {
        private readonly ProjektDbContext _context;

        public KundeController(ProjektDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var kunden = await _context.Kunden
                    .OrderBy(k => k.Kundenname)
                    .ToListAsync();
                return View(kunden);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading customers: {ex.Message}");
                return View(Array.Empty<Kunde>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KundeViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if customer number already exists
                    var existingKunde = await _context.Kunden
                        .FirstOrDefaultAsync(k => k.Kundennr == viewModel.Kundennr);
                    if (existingKunde != null)
                    {
                        ModelState.AddModelError("Kundennr", "This customer number already exists!");
                        return View(viewModel);
                    }
                    var kunde = new Kunde
                    {
                        Kundennr = viewModel.Kundennr,
                        Kundenname = viewModel.Kundenname
                    };
                    _context.Kunden.Add(kunde);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving: {ex.Message}");
                }
            }
            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var kunde = await _context.Kunden
                    .FirstOrDefaultAsync(k => k.Kundennr == id);
                if (kunde == null)
                {
                    return NotFound();
                }
                var viewModel = new KundeViewModel
                {
                    Kundennr = kunde.Kundennr,
                    Kundenname = kunde.Kundenname
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                return NotFound($"Error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KundeViewModel viewModel)
        {
            if (id != viewModel.Kundennr)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var kunde = await _context.Kunden
                        .FirstOrDefaultAsync(k => k.Kundennr == id);
                    if (kunde == null)
                    {
                        return NotFound();
                    }
                    kunde.Kundenname = viewModel.Kundenname;
                    _context.Update(kunde);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await KundeExists(viewModel.Kundennr))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving: {ex.Message}");
                    return View(viewModel);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }

        // New method to display the delete confirmation page
        public async Task<IActionResult> Delete(int id)
        {
            var kunde = await _context.Kunden
                .FirstOrDefaultAsync(k => k.Kundennr == id);
            if (kunde == null)
            {
                return NotFound();
            }
            // Check if customer is associated with projects
            var projekte = await _context.Projekte
                .Where(p => p.Kundennummer == id)
                .ToListAsync();
            ViewBag.ProjekteCount = projekte.Count;
            ViewBag.HasDependencies = projekte.Count > 0;
            ViewBag.Projekte = projekte;
            return View(kunde);
        }

        // Method to confirm the deletion
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool deleteProjects = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var kunde = await _context.Kunden
                    .FirstOrDefaultAsync(k => k.Kundennr == id);
                if (kunde == null)
                {
                    return NotFound();
                }
                // If projects should be deleted, first delete all activities
                if (deleteProjects)
                {
                    // Find customer's projects
                    var projekteIds = await _context.Projekte
                        .Where(p => p.Kundennummer == id)
                        .Select(p => p.Projektnummer)
                        .ToListAsync();
                    // Delete activities for these projects
                    var aktivitaeten = await _context.Aktivitaeten
                        .Where(a => projekteIds.Contains(a.Projektnummer))
                        .ToListAsync();
                    _context.Aktivitaeten.RemoveRange(aktivitaeten);
                    // Delete tickets for these projects
                    var tickets = await _context.Tickets
                        .Where(t => t.ProjektID.HasValue && projekteIds.Contains(t.ProjektID.Value))
                        .ToListAsync();
                    _context.Tickets.RemoveRange(tickets);
                    // Delete kanban cards for these projects
                    var kanbanCards = await _context.KanbanCards
                        .Where(c => projekteIds.Contains(c.ProjektID))
                        .ToListAsync();
                    _context.KanbanCards.RemoveRange(kanbanCards);
                    // Delete kanban buckets for these projects
                    var kanbanBuckets = await _context.KanbanBuckets
                        .Where(b => b.ProjektID.HasValue && projekteIds.Contains(b.ProjektID.Value))
                        .ToListAsync();
                    _context.KanbanBuckets.RemoveRange(kanbanBuckets);
                    // Delete projects
                    var projekte = await _context.Projekte
                        .Where(p => p.Kundennummer == id)
                        .ToListAsync();
                    _context.Projekte.RemoveRange(projekte);
                }
                else
                {
                    // Check if customer is associated with projects
                    var hatProjekte = await _context.Projekte.AnyAsync(p => p.Kundennummer == id);
                    if (hatProjekte)
                    {
                        TempData["ErrorMessage"] = "The customer cannot be deleted because there are still projects assigned to them. Please delete the projects first or select 'Delete all associated projects'.";
                        return RedirectToAction(nameof(Delete), new { id });
                    }
                }
                // Delete customer
                _context.Kunden.Remove(kunde);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Error deleting customer: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        private async Task<bool> KundeExists(int id)
        {
            return await _context.Kunden.AnyAsync(k => k.Kundennr == id);
        }
    }
}