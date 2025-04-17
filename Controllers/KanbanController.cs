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
        public async Task<IActionResult> Board(int id)
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

                // Get all cards for the project
                var karten = await _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .Where(c => c.ProjektID == id)
                    .OrderBy(c => c.Position)
                    .ToListAsync();

                var mitarbeiter = await _context.Mitarbeiter
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
                    MeineAufgaben = new List<KanbanCard>()  // Initialization to avoid null reference
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
        
        // Refresh cards via AJAX
        [HttpPost]
        public async Task<IActionResult> RefreshCards(int projektId, int? assignedTo = null, DateTime? dueDate = null)
        {
            try
            {
                // Create DTO objects to avoid circular references
                var dtoCards = new Dictionary<int, List<CardDTO>>();

                // Build query for cards
                var query = _context.KanbanCards
                    .Include(c => c.Bucket)
                    .Include(c => c.ZugewiesenAnMitarbeiter)
                    .Where(c => c.ProjektID == projektId);
                
                // Apply filters
                if (assignedTo.HasValue)
                {
                    query = query.Where(c => c.ZugewiesenAn == assignedTo.Value);
                }
                
                if (dueDate.HasValue)
                {
                    query = query.Where(c => 
                        c.FaelligAm.HasValue && c.FaelligAm.Value.Date == dueDate.Value.Date);
                }
                
                // Get filtered cards and map to DTOs
                var cards = await query.OrderBy(c => c.Position).ToListAsync();
                
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
                
                return Json(new { success = true, cards = dtoCards });
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
                if (bucket != null && bucket.Name.ToLower().Contains("erledigt"))
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

        // Helper method to load form data
        private async Task LoadFormData(KanbanCardViewModel viewModel)
        {
            try
            {
                viewModel.Buckets = await _context.KanbanBuckets
                    .Where(b => b.ProjektID == viewModel.ProjektID || (b.ProjektID == null && b.IstStandard))
                    .OrderBy(b => b.Reihenfolge)
                    .ToListAsync();
                    
                viewModel.Mitarbeiter = await _context.Mitarbeiter
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
        public MitarbeiterDTO ZugewiesenAnMitarbeiter { get; set; }
    }

    public class MitarbeiterDTO
    {
        public int MitarbeiterNr { get; set; }
        public string Vorname { get; set; }
        public string Name { get; set; }
    }
}