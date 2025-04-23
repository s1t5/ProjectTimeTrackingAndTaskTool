using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;
using System;
using System.Text.Json;

namespace ProjektZeiterfassung.Controllers
{
    public class KanbanController : Controller
    {
        private readonly ProjektDbContext _context;
        // Cookie name for employee number - imported from TimeTrackingController
        private const string MitarbeiterNrCookieName = "LastMitarbeiterNr";

        public KanbanController(ProjektDbContext context)
        {
            _context = context;
        }

        // Display projects with Kanban
        public async Task<IActionResult> Index()
        {
            try
            {
                var projekte = await _context.Projekte
                    .Include(p => p.Kunde)
                    .OrderBy(p => p.Projektbezeichnung)
                    .ToListAsync();

                // Count tasks for each project
                var projektTaskCounts = await _context.KanbanCards
                    .Where(c => !c.Erledigt)
                    .GroupBy(c => c.ProjektID)
                    .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

                ViewBag.ProjektTaskCounts = projektTaskCounts;

                // Ensure ViewBag properties are always initialized
                ViewBag.AssignedTasks = new List<KanbanCard>();
                ViewBag.MitarbeiterNr = null;

                // Try to read employee number from cookie
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterNr))
                {
                    ViewBag.MitarbeiterNr = parsedMitarbeiterNr;
                    // Load assigned tasks for overview
                    var assignedTasks = await _context.KanbanCards
                        .Include(c => c.Projekt)
                        .Include(c => c.Bucket)
                        .Where(c => c.ZugewiesenAn == parsedMitarbeiterNr && !c.Erledigt && c.FaelligAm.HasValue)
                        .OrderBy(c => c.FaelligAm)
                        .Take(10)
                        .ToListAsync();

                    if (assignedTasks.Any())
                    {
                        ViewBag.AssignedTasks = assignedTasks;
                    }
                }

                return View(projekte);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading projects: {ex.Message}");
                return View(new List<Projekt>());
            }
        }

        // Adjust Board method
        public async Task<IActionResult> Board(int id, bool showArchived = false)
        {
            try
            {
                var projekt = await _context.Projekte
                    .FirstOrDefaultAsync(p => p.Projektnummer == id);
                if (projekt == null)
                {
                    return NotFound();
                }

                // Get project-specific buckets or default buckets
                var buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == id || (b.ProjektID == null && b.IstStandard))
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                // Apply project-specific buckets preferentially
                var finalBuckets = buckets
                    .GroupBy(b => b.Name)
                    .Select(g => g.OrderByDescending(b => b.ProjektID.HasValue).First())
                    .OrderBy(b => b.Reihenfolge)
                    .ToList();

                // Get all cards for the project, filter by archive status if required
                var kartenQuery = _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .Where(c => c.ProjektID == id);

                // Only show non-archived cards by default
                if (!showArchived)
                {
                    kartenQuery = kartenQuery.Where(c => !c.Erledigt);
                }

                var karten = await kartenQuery
                    .OrderBy(c => c.Position)
                    .ToListAsync();

                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => !m.Inactive) // Only active employees
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();

                var model = new KanbanBoardViewModel
                {
                    ProjektID = id,
                    ProjektName = projekt.Projektbezeichnung,
                    BoardGUID = projekt.BoardGUID,
                    Buckets = finalBuckets,
                    Karten = karten,
                    Mitarbeiter = mitarbeiter,
                    MeineAufgaben = new List<KanbanCard>(),  // Initialization to avoid null reference
                    ShowArchivedCards = showArchived
                };

                // Read employee number and get current filter settings from query string
                int? mitarbeiterNr = null;
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
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
                ModelState.AddModelError("", $"Error loading Kanban board: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> RefreshCards(int projektId, bool showArchived = false, int? assignedTo = null, DateTime? dueDate = null)
        {
            try
            {
                // Create DTO objects to avoid circular references
                var dtoCards = new Dictionary<int, List<CardDTO>>();

                // Build query for cards
                var baseQuery = _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .Where(c => c.ProjektID == projektId)
                    .AsQueryable();

                // Apply archive filter if needed - only if showArchived is false
                if (!showArchived)
                {
                    baseQuery = baseQuery.Where(c => !c.Erledigt);
                }

                // Apply other filters
                if (assignedTo.HasValue)
                {
                    baseQuery = baseQuery.Where(c => c.ZugewiesenAn == assignedTo.Value);
                }
                if (dueDate.HasValue)
                {
                    baseQuery = baseQuery.Where(c =>
                        c.FaelligAm.HasValue && c.FaelligAm.Value.Date == dueDate.Value.Date);
                }

                // Get filtered cards and map to DTOs
                var cards = await baseQuery.OrderBy(c => c.Position).ToListAsync();

                // Group by bucket
                var cardsByBucket = cards.GroupBy(c => c.BucketID);

                foreach (var group in cardsByBucket)
                {
                    var cardDtos = new List<CardDTO>();
                    foreach (var card in group)
                    {
                        cardDtos.Add(new CardDTO
                        {
                            CardID = card.CardID,
                            Titel = card.Titel,
                            Beschreibung = card.Beschreibung,
                            BucketID = card.BucketID,
                            Position = card.Position,
                            Prioritaet = card.Prioritaet,
                            FaelligAm = card.FaelligAm,
                            Erledigt = card.Erledigt,
                            Storypoints = card.Storypoints,
                            ZugewiesenAnMitarbeiter = card.ZugewiesenAnMitarbeiter != null ?
                                new MitarbeiterDTO
                                {
                                    MitarbeiterNr = card.ZugewiesenAnMitarbeiter.MitarbeiterNr,
                                    Vorname = card.ZugewiesenAnMitarbeiter.Vorname,
                                    Name = card.ZugewiesenAnMitarbeiter.Name
                                } : null
                        });
                    }
                    dtoCards.Add(group.Key, cardDtos);
                }

                return Json(new { success = true, cards = dtoCards, showArchived = showArchived });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Manage buckets
        public async Task<IActionResult> Buckets(int id)
        {
            try
            {
                var projekt = await _context.Projekte
                    .FirstOrDefaultAsync(p => p.Projektnummer == id);

                if (projekt == null)
                {
                    return NotFound();
                }

                // Get all project-specific buckets
                var buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == id)
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                // Get all default buckets for comparison
                var standardBuckets = await _context.KanbanBuckets
                    .Where(b => b.IstStandard)
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                ViewBag.StandardBuckets = standardBuckets;
                ViewBag.ProjektID = id;
                ViewBag.ProjektName = projekt.Projektbezeichnung;
                ViewBag.BoardGUID = projekt.BoardGUID;

                return View(buckets);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading buckets: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        // Create bucket
        public IActionResult CreateBucket(int projektId)
        {
            var viewModel = new KanbanBucketViewModel
            {
                ProjektID = projektId,
                Reihenfolge = 100, // Default order
                Farbe = "#6c757d"  // Default: gray
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBucket(KanbanBucketViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var bucket = new KanbanBucket
                    {
                        Name = viewModel.Name,
                        Reihenfolge = viewModel.Reihenfolge,
                        Farbe = viewModel.Farbe ?? "#6c757d", // Default gray if none selected
                        ProjektID = viewModel.ProjektID,
                        IstStandard = false
                    };
                    _context.KanbanBuckets.Add(bucket);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Buckets), new { id = viewModel.ProjektID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving: {ex.Message}");
                }
            }

            return View(viewModel);
        }

        // Edit bucket
        public async Task<IActionResult> EditBucket(int id)
        {
            try
            {
                var bucket = await _context.KanbanBuckets.FindAsync(id);
                if (bucket == null)
                {
                    return NotFound();
                }

                var viewModel = new KanbanBucketViewModel
                {
                    BucketID = bucket.BucketID,
                    Name = bucket.Name,
                    Reihenfolge = bucket.Reihenfolge,
                    Farbe = bucket.Farbe,
                    ProjektID = bucket.ProjektID.Value
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading bucket: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBucket(int id, KanbanBucketViewModel viewModel)
        {
            if (id != viewModel.BucketID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var bucket = await _context.KanbanBuckets.FindAsync(id);
                    if (bucket == null)
                    {
                        return NotFound();
                    }

                    bucket.Name = viewModel.Name;
                    bucket.Reihenfolge = viewModel.Reihenfolge;
                    bucket.Farbe = viewModel.Farbe;
                    _context.Update(bucket);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Buckets), new { id = viewModel.ProjektID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await KanbanBucketExists(viewModel.BucketID))
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
                }
            }

            return View(viewModel);
        }

        // Delete bucket
        public async Task<IActionResult> DeleteBucket(int id)
        {
            try
            {
                var bucket = await _context.KanbanBuckets
                    .Include(b => b.Karten)
                    .FirstOrDefaultAsync(b => b.BucketID == id);

                if (bucket == null)
                {
                    return NotFound();
                }

                // Check if bucket has assigned cards
                if (bucket.Karten != null && bucket.Karten.Any())
                {
                    TempData["ErrorMessage"] = "Bucket cannot be deleted as it still has assigned cards.";
                    return RedirectToAction(nameof(Buckets), new { id = bucket.ProjektID });
                }

                int projektId = bucket.ProjektID.Value;
                _context.KanbanBuckets.Remove(bucket);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Buckets), new { id = projektId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error deleting bucket: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        // Create card
        public async Task<IActionResult> CreateCard(int projektId, int? bucketId = null)
        {
            try
            {
                var buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == projektId || (b.ProjektID == null && b.IstStandard))
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                var finalBuckets = buckets
                    .GroupBy(b => b.Name)
                    .Select(g => g.OrderByDescending(b => b.ProjektID.HasValue).First())
                    .OrderBy(b => b.Reihenfolge)
                    .ToList();

                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => !m.Inactive) // Only active employees
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();

                if (!mitarbeiter.Any())
                {
                    ModelState.AddModelError("", "Employees must be created before creating cards.");
                    return RedirectToAction(nameof(Board), new { id = projektId });
                }

                // Fallback to first bucket if specified doesn't exist
                if (bucketId.HasValue && !finalBuckets.Any(b => b.BucketID == bucketId))
                {
                    bucketId = finalBuckets.FirstOrDefault()?.BucketID;
                }

                // Get current employee number from cookie
                int? currentMitarbeiterId = null;
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterNr))
                {
                    currentMitarbeiterId = parsedMitarbeiterNr;
                }

                var viewModel = new KanbanCardViewModel
                {
                    ProjektID = projektId,
                    BucketID = bucketId ?? finalBuckets.FirstOrDefault()?.BucketID ?? 0,
                    FaelligAm = DateTime.Today.AddDays(7), // Default: 1 week
                    Buckets = finalBuckets,
                    Mitarbeiter = mitarbeiter,
                    Prioritaet = 1, // Default: Normal
                    ErstelltVon = currentMitarbeiterId ?? mitarbeiter.FirstOrDefault()?.MitarbeiterNr,
                    ZugewiesenAn = currentMitarbeiterId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating card: {ex.Message}");
                return RedirectToAction(nameof(Board), new { id = projektId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCard(KanbanCardViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get highest position in target bucket
                    int maxPosition = 0;
                    var cards = await _context.KanbanCards
                        .Where(c => c.BucketID == viewModel.BucketID)
                        .ToListAsync();

                    if (cards.Any())
                    {
                        maxPosition = cards.Max(c => c.Position);
                    }

                    int erstelltVon = viewModel.ErstelltVon ?? 0;
                    if (erstelltVon == 0)
                    {
                        var mitarbeiter = await _context.Mitarbeiter.FirstOrDefaultAsync();
                        if (mitarbeiter == null)
                        {
                            ModelState.AddModelError("", "No employee found for creation. Please create an employee first.");
                            await LoadFormData(viewModel);
                            return View(viewModel);
                        }
                        erstelltVon = mitarbeiter.MitarbeiterNr;
                    }

                    var card = new KanbanCard
                    {
                        Titel = viewModel.Titel,
                        Beschreibung = viewModel.Beschreibung,
                        BucketID = viewModel.BucketID,
                        ProjektID = viewModel.ProjektID,
                        ErstelltAm = DateTime.Now,
                        ErstelltVon = erstelltVon,
                        ZugewiesenAn = viewModel.ZugewiesenAn,
                        Prioritaet = viewModel.Prioritaet,
                        FaelligAm = viewModel.FaelligAm,
                        Storypoints = viewModel.Storypoints,  // Neu hinzugefügt
                        Erledigt = false,
                        Position = maxPosition + 1
                    };

                    _context.KanbanCards.Add(card);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Board), new { id = viewModel.ProjektID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error saving card: {ex.Message}");
                }
            }

            await LoadFormData(viewModel);
            return View(viewModel);
        }

        // Edit card
        public async Task<IActionResult> EditCard(int id)
        {
            try
            {
                var card = await _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.Projekt)
                    .FirstOrDefaultAsync(c => c.CardID == id);

                if (card == null)
                {
                    return NotFound();
                }

                var buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == card.ProjektID || (b.ProjektID == null && b.IstStandard))
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                var finalBuckets = buckets
                    .GroupBy(b => b.Name)
                    .Select(g => g.OrderByDescending(b => b.ProjektID.HasValue).First())
                    .OrderBy(b => b.Reihenfolge)
                    .ToList();

                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => !m.Inactive) // Only active employees
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();

                var viewModel = new KanbanCardViewModel
                {
                    CardID = card.CardID,
                    Titel = card.Titel,
                    Beschreibung = card.Beschreibung,
                    BucketID = card.BucketID,
                    ProjektID = card.ProjektID,
                    ProjektBezeichnung = card.Projekt?.Projektbezeichnung,
                    ErstelltVon = card.ErstelltVon,
                    ZugewiesenAn = card.ZugewiesenAn,
                    Prioritaet = card.Prioritaet,
                    FaelligAm = card.FaelligAm,
                    Storypoints = card.Storypoints,
                    Buckets = finalBuckets,
                    Mitarbeiter = mitarbeiter
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading card: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCard(int id, KanbanCardViewModel viewModel)
        {
            if (id != viewModel.CardID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var card = await _context.KanbanCards.FindAsync(id);
                    if (card == null)
                    {
                        return NotFound();
                    }

                    if (card.BucketID != viewModel.BucketID)
                    {
                        int maxPosition = 0;
                        var cards = await _context.KanbanCards
                            .Where(c => c.BucketID == viewModel.BucketID)
                            .ToListAsync();

                        if (cards.Any())
                        {
                            maxPosition = cards.Max(c => c.Position);
                        }

                        card.Position = maxPosition + 1;
                    }

                    card.Titel = viewModel.Titel;
                    card.Beschreibung = viewModel.Beschreibung;
                    card.BucketID = viewModel.BucketID;
                    card.ZugewiesenAn = viewModel.ZugewiesenAn;
                    card.Prioritaet = viewModel.Prioritaet;
                    card.FaelligAm = viewModel.FaelligAm;
                    card.Storypoints = viewModel.Storypoints;  // Neu hinzugefügt

                    _context.Update(card);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Board), new { id = viewModel.ProjektID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await KanbanCardExists(viewModel.CardID))
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
                }
            }

            await LoadFormData(viewModel);
            return View(viewModel);
        }

        // Delete card
        public async Task<IActionResult> DeleteCard(int id)
        {
            try
            {
                var card = await _context.KanbanCards
                    .Include(c => c.Projekt)
                    .FirstOrDefaultAsync(c => c.CardID == id);

                if (card == null)
                {
                    return NotFound();
                }

                int projektId = card.ProjektID;
                _context.KanbanCards.Remove(card);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Board), new { id = projektId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error deleting card: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> ArchiveCard(int id)
        {
            try
            {
                var card = await _context.KanbanCards.FindAsync(id);
                if (card == null)
                {
                    return NotFound(new { success = false, message = "Card not found" });
                }

                card.Erledigt = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Karte wiederherstellen
        [HttpPost]
        public async Task<IActionResult> UnarchiveCard(int id)
        {
            try
            {
                var card = await _context.KanbanCards.FindAsync(id);
                if (card == null)
                {
                    return NotFound(new { success = false, message = "Card not found" });
                }

                card.Erledigt = false;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Move card via drag & drop (AJAX)
        [HttpPost]
        public async Task<IActionResult> MoveCard([FromBody] KanbanCardMoveViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid data" });
            }

            try
            {
                var card = await _context.KanbanCards.FindAsync(model.CardID);
                if (card == null)
                {
                    return NotFound(new { success = false, message = "Card not found" });
                }

                int oldBucketId = card.BucketID;
                int oldPosition = card.Position;

                card.BucketID = model.NewBucketID;
                card.Position = model.NewPosition;

                var bucket = await _context.KanbanBuckets.FindAsync(model.NewBucketID);
                if (bucket != null &&
                   (bucket.Name.ToLower().Contains("erledigt") ||
                    bucket.Name.ToLower().Contains("done") ||
                    bucket.Name.ToLower().Contains("finished")))
                {
                    card.Erledigt = true;
                }

                if (oldBucketId == model.NewBucketID)
                {
                    if (oldPosition < model.NewPosition)
                    {
                        var cardsToUpdate = await _context.KanbanCards
                            .Where(c => c.BucketID == model.NewBucketID &&
                                     c.Position > oldPosition &&
                                     c.Position <= model.NewPosition &&
                                     c.CardID != model.CardID)
                            .ToListAsync();

                        foreach (var c in cardsToUpdate)
                        {
                            c.Position--;
                        }
                    }
                    else if (oldPosition > model.NewPosition)
                    {
                        var cardsToUpdate = await _context.KanbanCards
                            .Where(c => c.BucketID == model.NewBucketID &&
                                     c.Position >= model.NewPosition &&
                                     c.Position < oldPosition &&
                                     c.CardID != model.CardID)
                            .ToListAsync();

                        foreach (var c in cardsToUpdate)
                        {
                            c.Position++;
                        }
                    }
                }
                else
                {
                    var cardsInOldBucket = await _context.KanbanCards
                        .Where(c => c.BucketID == oldBucketId && c.Position > oldPosition)
                        .ToListAsync();

                    foreach (var c in cardsInOldBucket)
                    {
                        c.Position--;
                    }

                    var cardsInNewBucket = await _context.KanbanCards
                        .Where(c => c.BucketID == model.NewBucketID &&
                                 c.Position >= model.NewPosition &&
                                 c.CardID != model.CardID)
                        .ToListAsync();

                    foreach (var c in cardsInNewBucket)
                    {
                        c.Position++;
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Methode zum Hinzufügen von Kommentaren
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int cardId, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return BadRequest(new { success = false, message = "Comment text is required" });
            }

            try
            {
                int? mitarbeiterId = null;
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterId))
                {
                    mitarbeiterId = parsedMitarbeiterId;
                }
                if (!mitarbeiterId.HasValue)
                {
                    return BadRequest(new { success = false, message = "Employee not identified" });
                }

                var card = await _context.KanbanCards.FirstOrDefaultAsync(c => c.CardID == cardId);
                if (card == null)
                {
                    return NotFound(new { success = false, message = "Card not found" });
                }

                var kommentar = new KanbanComment
                {
                    CardID = cardId,
                    Comment = comment,
                    ErstelltVon = mitarbeiterId.Value,
                    ErstelltAm = DateTime.Now
                };

                _context.KanbanComments.Add(kommentar);
                await _context.SaveChangesAsync();

                // Laden des Mitarbeiters für die Antwort
                var mitarbeiter = await _context.Mitarbeiter.FindAsync(mitarbeiterId.Value);

                return Json(new
                {
                    success = true,
                    comment = new
                    {
                        commentId = kommentar.CommentID,
                        text = kommentar.Comment,
                        createdAt = kommentar.ErstelltAm.ToString("dd.MM.yyyy HH:mm"),
                        createdBy = mitarbeiter != null ? $"{mitarbeiter.Vorname} {mitarbeiter.Name}" : "Unknown"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Methode zum Bearbeiten von Kommentaren
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int commentId, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return BadRequest(new { success = false, message = "Comment text is required" });
            }

            try
            {
                var kommentar = await _context.KanbanComments.FindAsync(commentId);
                if (kommentar == null)
                {
                    return NotFound(new { success = false, message = "Comment not found" });
                }

                int? mitarbeiterId = null;
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterId))
                {
                    mitarbeiterId = parsedMitarbeiterId;
                }
                // Nur der Ersteller darf bearbeiten
                if (mitarbeiterId != kommentar.ErstelltVon)
                {
                    return Forbid();
                }

                kommentar.Comment = comment;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Methode zum Löschen von Kommentaren
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            try
            {
                var kommentar = await _context.KanbanComments.FindAsync(commentId);
                if (kommentar == null)
                {
                    return NotFound(new { success = false, message = "Comment not found" });
                }

                int? mitarbeiterId = null;
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int parsedMitarbeiterId))
                {
                    mitarbeiterId = parsedMitarbeiterId;
                }
                // Nur der Ersteller darf löschen
                if (mitarbeiterId != kommentar.ErstelltVon)
                {
                    return Forbid();
                }

                _context.KanbanComments.Remove(kommentar);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Methode zum Laden von Kommentaren einer Karte
        [HttpGet]
        public async Task<IActionResult> GetComments(int cardId)
        {
            try
            {
                var comments = await _context.KanbanComments
                    .Include(c => c.ErstelltVonMitarbeiter)
                    .Where(c => c.CardID == cardId)
                    .OrderByDescending(c => c.ErstelltAm)
                    .Select(c => new
                    {
                        commentId = c.CommentID,
                        text = c.Comment,
                        createdAt = c.ErstelltAm.ToString("dd.MM.yyyy HH:mm"),
                        createdBy = c.ErstelltVonMitarbeiter != null
                            ? $"{c.ErstelltVonMitarbeiter.Vorname} {c.ErstelltVonMitarbeiter.Name}"
                            : "Unknown",
                        createdById = c.ErstelltVon
                    })
                    .ToListAsync();

                return Json(new { success = true, comments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Update the LoadFormData method to filter out inactive employees
        private async Task LoadFormData(KanbanCardViewModel viewModel)
        {
            try
            {
                viewModel.Buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == viewModel.ProjektID || (b.ProjektID == null && b.IstStandard))
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();

                viewModel.Mitarbeiter = await _context.Mitarbeiter
                    .Where(m => !m.Inactive) // Only active employees
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading form data: {ex.Message}");
            }
        }

        private async Task<bool> KanbanBucketExists(int id)
        {
            return await _context.KanbanBuckets.AnyAsync(e => e.BucketID == id);
        }

        private async Task<bool> KanbanCardExists(int id)
        {
            return await _context.KanbanCards.AnyAsync(e => e.CardID == id);
        }
    }

    // DTO classes for serialization
    public class CardDTO
    {
        public int CardID { get; set; }
        public string Titel { get; set; }
        public string Beschreibung { get; set; }
        public int BucketID { get; set; }
        public int Position { get; set; }
        public int Prioritaet { get; set; }
        public DateTime? FaelligAm { get; set; }
        public string ProjektName { get; set; }
        public bool Erledigt { get; set; } // Fügen Sie dieses Feld hinzu
        public MitarbeiterDTO ZugewiesenAnMitarbeiter { get; set; }
        public int? Storypoints { get; set; }
        public List<CommentDTO> Comments { get; set; }
    }

    public class CommentDTO
    {
        public int CommentID { get; set; }
        public string Comment { get; set; }
        public int ErstelltVon { get; set; }
        public string ErstelltVonName { get; set; }
        public DateTime ErstelltAm { get; set; }
    }
    public class MitarbeiterDTO
    {
        public int MitarbeiterNr { get; set; }
        public string Vorname { get; set; }
        public string Name { get; set; }
    }
}