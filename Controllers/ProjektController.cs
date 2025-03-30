using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;

namespace ProjektZeiterfassung.Controllers
{
    public class ProjektController : Controller
    {
        private readonly ProjektDbContext _context;

        public ProjektController(ProjektDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var projekte = await _context.Projekte
                    .Include(p => p.Kunde)
                    .OrderBy(p => p.Projektbezeichnung)
                    .ToListAsync();

                return View(projekte);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading projects: {ex.Message}");
                return View(Array.Empty<Projekt>());
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new ProjektViewModel
                {
                    KundenListe = await _context.Kunden
                        .OrderBy(k => k.Kundenname)
                        .ToListAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading customers: {ex.Message}");
                return View(new ProjektViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjektViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var projekt = new Projekt
                    {
                        Projektbezeichnung = viewModel.Projektbezeichnung,
                        Bemerkung = viewModel.Bemerkung,
                        Kundennummer = viewModel.Kundennummer,
                        InkludierteStunden = viewModel.InkludierteStunden,
                        NachberechneteMinuten = viewModel.NachberechneteMinuten
                    };

                    _context.Projekte.Add(projekt);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving: {ex.Message}");
                }
            }

            try
            {
                viewModel.KundenListe = await _context.Kunden
                    .OrderBy(k => k.Kundenname)
                    .ToListAsync();
            }
            catch
            {
                // If an error occurs, just use an empty list
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var projekt = await _context.Projekte
                    .FirstOrDefaultAsync(p => p.Projektnummer == id);

                if (projekt == null)
                {
                    return NotFound();
                }

                var viewModel = new ProjektViewModel
                {
                    Projektnummer = projekt.Projektnummer,
                    Projektbezeichnung = projekt.Projektbezeichnung,
                    Bemerkung = projekt.Bemerkung,
                    Kundennummer = projekt.Kundennummer,
                    InkludierteStunden = projekt.InkludierteStunden,
                    NachberechneteMinuten = projekt.NachberechneteMinuten,
                    KundenListe = await _context.Kunden
                        .OrderBy(k => k.Kundenname)
                        .ToListAsync()
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
        public async Task<IActionResult> Edit(int id, ProjektViewModel viewModel)
        {
            if (id != viewModel.Projektnummer)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var projekt = await _context.Projekte
                        .FirstOrDefaultAsync(p => p.Projektnummer == id);

                    if (projekt == null)
                    {
                        return NotFound();
                    }

                    projekt.Projektbezeichnung = viewModel.Projektbezeichnung;
                    projekt.Bemerkung = viewModel.Bemerkung;
                    projekt.Kundennummer = viewModel.Kundennummer;
                    projekt.InkludierteStunden = viewModel.InkludierteStunden;
                    projekt.NachberechneteMinuten = viewModel.NachberechneteMinuten;

                    _context.Update(projekt);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await ProjektExists(viewModel.Projektnummer))
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

                    try
                    {
                        viewModel.KundenListe = await _context.Kunden
                            .OrderBy(k => k.Kundenname)
                            .ToListAsync();
                    }
                    catch
                    {
                        // If an error occurs, just use an empty list
                    }

                    return View(viewModel);
                }

                return RedirectToAction(nameof(Index));
            }

            try
            {
                viewModel.KundenListe = await _context.Kunden
                    .OrderBy(k => k.Kundenname)
                    .ToListAsync();
            }
            catch
            {
                // If an error occurs, just use an empty list
            }

            return View(viewModel);
        }

        // New method to display the delete confirmation page
        public async Task<IActionResult> Delete(int id)
        {
            var projekt = await _context.Projekte
                .Include(p => p.Kunde)
                .FirstOrDefaultAsync(p => p.Projektnummer == id);

            if (projekt == null)
            {
                return NotFound();
            }

            // Check for dependencies
            var aktivitaetenCount = await _context.Aktivitaeten
                .Where(a => a.Projektnummer == id)
                .CountAsync();

            var ticketsCount = await _context.Tickets
                .Where(t => t.ProjektID == id)
                .CountAsync();

            var kanbanCardsCount = await _context.KanbanCards
                .Where(c => c.ProjektID == id)
                .CountAsync();

            ViewBag.AktivitaetenCount = aktivitaetenCount;
            ViewBag.TicketsCount = ticketsCount;
            ViewBag.KanbanCardsCount = kanbanCardsCount;
            ViewBag.HasDependencies = aktivitaetenCount > 0 || ticketsCount > 0 || kanbanCardsCount > 0;

            return View(projekt);
        }

        // Method to confirm the deletion
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool deleteRelated = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var projekt = await _context.Projekte
                    .FirstOrDefaultAsync(p => p.Projektnummer == id);

                if (projekt == null)
                {
                    return NotFound();
                }

                if (deleteRelated)
                {
                    // Delete activities for this project
                    var aktivitaeten = await _context.Aktivitaeten
                        .Where(a => a.Projektnummer == id)
                        .ToListAsync();

                    _context.Aktivitaeten.RemoveRange(aktivitaeten);

                    // Delete tickets for this project
                    var tickets = await _context.Tickets
                        .Where(t => t.ProjektID == id)
                        .ToListAsync();

                    _context.Tickets.RemoveRange(tickets);

                    // Delete kanban cards for this project
                    var kanbanCards = await _context.KanbanCards
                        .Where(c => c.ProjektID == id)
                        .ToListAsync();

                    _context.KanbanCards.RemoveRange(kanbanCards);

                    // Delete kanban buckets for this project
                    var kanbanBuckets = await _context.KanbanBuckets
                        .Where(b => b.ProjektID == id)
                        .ToListAsync();

                    _context.KanbanBuckets.RemoveRange(kanbanBuckets);
                }
                else
                {
                    // Check for dependencies
                    var hasActivities = await _context.Aktivitaeten.AnyAsync(a => a.Projektnummer == id);
                    var hasTickets = await _context.Tickets.AnyAsync(t => t.ProjektID == id);
                    var hasKanbanCards = await _context.KanbanCards.AnyAsync(c => c.ProjektID == id);

                    if (hasActivities || hasTickets || hasKanbanCards)
                    {
                        TempData["ErrorMessage"] = "The project cannot be deleted because there are still activities, tickets or kanban cards assigned to it. Please delete these elements first or choose 'Delete all related elements'.";
                        return RedirectToAction(nameof(Delete), new { id });
                    }
                }

                // Delete project
                _context.Projekte.Remove(projekt);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Error deleting project: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        private async Task<bool> ProjektExists(int id)
        {
            return await _context.Projekte.AnyAsync(p => p.Projektnummer == id);
        }
    }
}