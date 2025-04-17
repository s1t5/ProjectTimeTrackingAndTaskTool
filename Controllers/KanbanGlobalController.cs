using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;
using System.Linq;

namespace ProjektZeiterfassung.Controllers
{
    public class KanbanGlobalController : Controller
    {
        private readonly ProjektDbContext _context;

        public KanbanGlobalController(ProjektDbContext context)
        {
            _context = context;
        }

        // Update der GlobalBoard-Methode
        public async Task<IActionResult> GlobalBoard(bool showArchived = false)
        {
            try
            {
                // Get all unique bucket names across projects
                var bucketsByName = await _context.KanbanBuckets
                    .Where(b => b.ProjektID.HasValue || b.IstStandard)
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                // Group buckets by name, prioritizing project-specific ones
                var uniqueBuckets = bucketsByName
                    .GroupBy(b => b.Name)
                    .Select(g => g.OrderByDescending(b => b.ProjektID.HasValue).First())
                    .OrderBy(b => b.Reihenfolge)
                    .ToList();

                // Build base query for all cards
                var baseQuery = _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.Projekt)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .AsQueryable(); // Wichtig: AsQueryable() klarstellen

                // Apply archive filter if needed
                if (!showArchived)
                {
                    baseQuery = baseQuery.Where(c => !c.Erledigt);
                }

                // Execute the final query
                var karten = await baseQuery
                    .OrderBy(c => c.Position)
                    .ToListAsync();

                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => !m.Inactive) // Only active employees
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();

                var model = new KanbanBoardViewModel
                {
                    ProjektID = 0, // Special ID for global board
                    ProjektName = "Global Board - All Projects",
                    BoardGUID = "global",
                    Buckets = uniqueBuckets,
                    Karten = karten,
                    Mitarbeiter = mitarbeiter,
                    MeineAufgaben = new List<KanbanCard>(),
                    ShowArchivedCards = showArchived
                };

                // Try to read employee number from cookie
                int? mitarbeiterNr = null;
                if (Request.Cookies.TryGetValue(ZeiterfassungController.MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterNr))
                {
                    mitarbeiterNr = parsedMitarbeiterNr;
                    model.AktuelleMitarbeiterNr = parsedMitarbeiterNr;
                }

                // Apply filters if provided
                int? filterAssigned = null;
                if (Request.Query.TryGetValue("assigned", out var assignedValues) &&
                    int.TryParse(assignedValues.FirstOrDefault(), out int parsedAssigned))
                {
                    filterAssigned = parsedAssigned;
                    model.FilterAssignedTo = parsedAssigned;
                }

                DateTime? filterDueDate = null;
                if (Request.Query.TryGetValue("due", out var dueValues) &&
                    DateTime.TryParse(dueValues.FirstOrDefault(), out DateTime parsedDueDate))
                {
                    filterDueDate = parsedDueDate;
                    model.FilterDueDate = parsedDueDate;
                }

                // Apply filters to cards if needed
                if (filterAssigned.HasValue || filterDueDate.HasValue)
                {
                    var filteredCards = karten.ToList();
                    if (filterAssigned.HasValue)
                    {
                        filteredCards = filteredCards.Where(c => c.ZugewiesenAn == filterAssigned.Value).ToList();
                    }
                    if (filterDueDate.HasValue)
                    {
                        filteredCards = filteredCards.Where(c =>
                            c.FaelligAm.HasValue && c.FaelligAm.Value.Date == filterDueDate.Value.Date).ToList();
                    }
                    model.Karten = filteredCards;
                }

                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading Global Kanban board: {ex.Message}");
                return RedirectToAction("Index", "Kanban");
            }
        }

        // Refresh cards via AJAX
        [HttpPost]
        public async Task<IActionResult> RefreshGlobalCards(bool showArchived = false, int? assignedTo = null, DateTime? dueDate = null)
        {
            try
            {
                // Create DTO objects to avoid circular references
                var dtoCards = new Dictionary<string, List<CardDTO>>();

                // Start with a base query
                var baseQuery = _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.Projekt)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .AsQueryable();

                // Apply archive filter
                if (!showArchived)
                {
                    baseQuery = baseQuery.Where(c => !c.Erledigt);
                }

                // Apply other filters...
                if (assignedTo.HasValue)
                {
                    baseQuery = baseQuery.Where(c => c.ZugewiesenAn == assignedTo.Value);
                }
                if (dueDate.HasValue)
                {
                    baseQuery = baseQuery.Where(c =>
                        c.FaelligAm.HasValue && c.FaelligAm.Value.Date == dueDate.Value.Date);
                }

                // Execute the query
                var cards = await baseQuery.OrderBy(c => c.Position).ToListAsync();

                // Get all buckets grouped by name
                var bucketsByName = await _context.KanbanBuckets.ToListAsync();

                var bucketNameMap = bucketsByName
                    .GroupBy(b => b.Name ?? "")
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(b => b.BucketID).ToList()
                    );

                // Group cards by bucket name
                foreach (var card in cards)
                {
                    var bucketName = card.Bucket?.Name ?? "Unknown";

                    if (!dtoCards.ContainsKey(bucketName))
                    {
                        dtoCards[bucketName] = new List<CardDTO>();
                    }

                    dtoCards[bucketName].Add(new CardDTO
                    {
                        CardID = card.CardID,
                        Titel = card.Titel ?? "",
                        Beschreibung = card.Beschreibung ?? "",
                        BucketID = card.BucketID,
                        Position = card.Position,
                        Prioritaet = card.Prioritaet,
                        FaelligAm = card.FaelligAm,
                        ProjektName = card.Projekt?.Projektbezeichnung ?? "",
                        Erledigt = card.Erledigt,
                        ZugewiesenAnMitarbeiter = card.ZugewiesenAnMitarbeiter != null ?
                            new MitarbeiterDTO
                            {
                                MitarbeiterNr = card.ZugewiesenAnMitarbeiter.MitarbeiterNr,
                                Vorname = card.ZugewiesenAnMitarbeiter.Vorname ?? "",
                                Name = card.ZugewiesenAnMitarbeiter.Name ?? ""
                            } : null
                    });
                }

                return Json(new { success = true, cards = dtoCards, bucketMap = bucketNameMap });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}