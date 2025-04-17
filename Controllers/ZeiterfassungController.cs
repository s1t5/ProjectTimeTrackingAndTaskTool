using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;

namespace ProjektZeiterfassung.Controllers
{
    public class ZeiterfassungController : Controller
    {
        private readonly ProjektDbContext _context;
        // Cookie name for employee number - public to be used by other controllers
        public const string MitarbeiterNrCookieName = "LastMitarbeiterNr";

        public ZeiterfassungController(ProjektDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // More efficient and NULL-safe query
                var projects = await _context.Projekte
                    .Select(p => new Projekt
                    {
                        Projektnummer = p.Projektnummer,
                        Projektbezeichnung = p.Projektbezeichnung ?? "",
                        Bemerkung = p.Bemerkung,
                        Kundennummer = p.Kundennummer,
                        InkludierteStunden = p.InkludierteStunden,
                        NachberechneteMinuten = p.NachberechneteMinuten,
                        Kunde = p.Kunde != null ? new Kunde
                        {
                            Kundennr = p.Kunde.Kundennr,
                            Kundenname = p.Kunde.Kundenname ?? ""
                        } : null
                    })
                    .OrderBy(p => p.Projektbezeichnung)
                    .ToListAsync();

                var viewModel = new ZeiterfassungViewModel
                {
                    ProjektListe = projects,
                    Datum = DateTime.Today,
                    Berechnen = true // Default is enabled
                };

                // Read stored employee number from cookie
                if (Request.Cookies.TryGetValue(MitarbeiterNrCookieName, out string mitarbeiterNrStr) &&
                    int.TryParse(mitarbeiterNrStr, out int mitarbeiterNr))
                {
                    viewModel.MitarbeiterNr = mitarbeiterNr;
                    // Load employee data immediately
                    var mitarbeiter = await _context.Mitarbeiter
                        .FirstOrDefaultAsync(m => m.MitarbeiterNr == mitarbeiterNr);
                    if (mitarbeiter != null)
                    {
                        viewModel.AktuellerMitarbeiter = mitarbeiter;

                        // NEUE FUNKTIONALITÄT: Lade anstehende Aufgaben des Mitarbeiters
                        var upcomingTasks = await _context.KanbanCards
                            .Include(c => c.Projekt)
                            .Include(c => c.Bucket)
                            .Where(c => c.ZugewiesenAn == mitarbeiterNr &&
                                       !c.Erledigt &&
                                       c.FaelligAm.HasValue)
                            .OrderBy(c => c.FaelligAm)
                            .Take(4)
                            .ToListAsync();

                        viewModel.AnstehendeAufgaben = upcomingTasks;
                    }
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log error for later analysis
                Console.WriteLine($"Error loading projects: {ex.Message}");
                // Fallback solution to still display the page
                var viewModel = new ZeiterfassungViewModel
                {
                    ProjektListe = new List<Projekt>(),
                    Datum = DateTime.Today,
                    Berechnen = true // Default is enabled
                };
                ModelState.AddModelError(string.Empty, "Error loading project data. Please try again later.");
                return View(viewModel);
            }
        }

        // New method to set cookie directly after employee verification
        [HttpPost]
        public async Task<IActionResult> VerifyMitarbeiter(int mitarbeiterNr)
        {
            var mitarbeiter = await _context.Mitarbeiter
                .FirstOrDefaultAsync(m => m.MitarbeiterNr == mitarbeiterNr && !m.Inactive);
            if (mitarbeiter == null)
            {
                return Json(new { success = false });
            }

            // Set cookie directly (valid for 30 days)
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };
            Response.Cookies.Append(MitarbeiterNrCookieName, mitarbeiterNr.ToString(), cookieOptions);

            return Json(new
            {
                success = true,
                name = mitarbeiter.Name ?? "",
                vorname = mitarbeiter.Vorname ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ZeiterfassungViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var mitarbeiter = await _context.Mitarbeiter
                        .FirstOrDefaultAsync(m => m.MitarbeiterNr == viewModel.MitarbeiterNr);

                    if (mitarbeiter == null)
                    {
                        ModelState.AddModelError("MitarbeiterNr", "Employee not found!");
                        viewModel.ProjektListe = await _context.Projekte
                            .OrderBy(p => p.Projektbezeichnung)
                            .ToListAsync();
                        return View(viewModel);
                    }

                    var aktivitaet = new Aktivitaet
                    {
                        Projektnummer = viewModel.Projektnummer,
                        Beschreibung = viewModel.Beschreibung,
                        Datum = viewModel.Datum,
                        Start = viewModel.Startzeit,
                        Ende = viewModel.Endzeit,
                        Mitarbeiter = viewModel.MitarbeiterNr,
                        Berechnen = viewModel.Berechnen ? (byte)1 : (byte)0,
                        Anfahrt = viewModel.Anfahrt ? (byte)1 : (byte)0
                    };

                    _context.Aktivitaeten.Add(aktivitaet);
                    await _context.SaveChangesAsync();

                    // Store employee number in cookie (valid for 30 days)
                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(30),
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax
                    };
                    Response.Cookies.Append(MitarbeiterNrCookieName, viewModel.MitarbeiterNr.ToString(), cookieOptions);

                    return RedirectToAction(nameof(Success), new { id = aktivitaet.AktivitaetsID });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error saving: {ex.Message}");
                }
            }

            try
            {
                viewModel.ProjektListe = await _context.Projekte
                    .OrderBy(p => p.Projektbezeichnung)
                    .ToListAsync();
            }
            catch
            {
                viewModel.ProjektListe = new List<Projekt>();
                ModelState.AddModelError(string.Empty, "Error loading project data.");
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var aktivitaet = await _context.Aktivitaeten
                    .Include(a => a.Projekt)
                    .Include(a => a.MitarbeiterObj)
                    .FirstOrDefaultAsync(a => a.AktivitaetsID == id);

                if (aktivitaet == null)
                {
                    return NotFound();
                }

                var viewModel = new AktivitaetEditViewModel
                {
                    AktivitaetID = aktivitaet.AktivitaetsID,
                    MitarbeiterNr = aktivitaet.Mitarbeiter,
                    MitarbeiterName = $"{aktivitaet.MitarbeiterObj?.Vorname ?? ""} {aktivitaet.MitarbeiterObj?.Name ?? ""}",
                    Projektnummer = aktivitaet.Projektnummer,
                    ProjektBezeichnung = aktivitaet.Projekt?.Projektbezeichnung ?? "",
                    Datum = aktivitaet.Datum,
                    Startzeit = aktivitaet.Start,
                    Endzeit = aktivitaet.Ende,
                    Beschreibung = aktivitaet.Beschreibung,
                    Berechnen = aktivitaet.Berechnen == 1,
                    Anfahrt = aktivitaet.Anfahrt == 1
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
        public async Task<IActionResult> Edit(int id, AktivitaetEditViewModel viewModel)
        {
            if (id != viewModel.AktivitaetID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var aktivitaet = await _context.Aktivitaeten
                        .FirstOrDefaultAsync(a => a.AktivitaetsID == id);

                    if (aktivitaet == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    aktivitaet.Datum = viewModel.Datum;
                    aktivitaet.Start = viewModel.Startzeit;
                    aktivitaet.Ende = viewModel.Endzeit;
                    aktivitaet.Beschreibung = viewModel.Beschreibung;
                    aktivitaet.Berechnen = viewModel.Berechnen ? (byte)1 : (byte)0;
                    aktivitaet.Anfahrt = viewModel.Anfahrt ? (byte)1 : (byte)0;

                    _context.Update(aktivitaet);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(EditSuccess), new { id = aktivitaet.AktivitaetsID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await AktivitaetExists(viewModel.AktivitaetID))
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
                    ModelState.AddModelError(string.Empty, $"Error saving: {ex.Message}");
                    return View(viewModel);
                }
            }

            return View(viewModel);
        }

        // Methods to delete an activity
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var aktivitaet = await _context.Aktivitaeten
                    .Include(a => a.Projekt)
                    .Include(a => a.MitarbeiterObj)
                    .FirstOrDefaultAsync(a => a.AktivitaetsID == id);

                if (aktivitaet == null)
                {
                    return NotFound();
                }

                var viewModel = new AktivitaetDeleteViewModel
                {
                    AktivitaetID = aktivitaet.AktivitaetsID,
                    MitarbeiterName = $"{aktivitaet.MitarbeiterObj?.Vorname ?? ""} {aktivitaet.MitarbeiterObj?.Name ?? ""}",
                    ProjektBezeichnung = aktivitaet.Projekt?.Projektbezeichnung ?? "",
                    Datum = aktivitaet.Datum,
                    Startzeit = aktivitaet.Start,
                    Endzeit = aktivitaet.Ende,
                    Beschreibung = aktivitaet.Beschreibung,
                    Berechnen = aktivitaet.Berechnen == 1,
                    Anfahrt = aktivitaet.Anfahrt == 1
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                return NotFound($"Error: {ex.Message}");
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var aktivitaet = await _context.Aktivitaeten.FindAsync(id);
                if (aktivitaet == null)
                {
                    return NotFound();
                }

                _context.Aktivitaeten.Remove(aktivitaet);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(DeleteSuccess));
            }
            catch (Exception ex)
            {
                return RedirectToAction(nameof(Error), new { message = $"Error deleting: {ex.Message}" });
            }
        }

        public IActionResult Success(int id)
        {
            return View(id);
        }

        public IActionResult EditSuccess(int id)
        {
            return View(id);
        }

        public IActionResult DeleteSuccess()
        {
            return View();
        }

        public IActionResult Error(string message)
        {
            return View(model: message);
        }

        public async Task<IActionResult> GetMitarbeiterInfo(int mitarbeiterNr)
        {
            var mitarbeiter = await _context.Mitarbeiter
                .FirstOrDefaultAsync(m => m.MitarbeiterNr == mitarbeiterNr && !m.Inactive);
            if (mitarbeiter == null)
            {
                return Json(new { success = false });
            }

            return Json(new
            {
                success = true,
                name = mitarbeiter.Name ?? "",
                vorname = mitarbeiter.Vorname ?? ""
            });
        }

        public async Task<IActionResult> GetProjektAktivitaeten(int projektnummer)
        {
            try
            {
                var aktivitaeten = await _context.Aktivitaeten
                    .Include(a => a.MitarbeiterObj)
                    .Where(a => a.Projektnummer == projektnummer)
                    .OrderByDescending(a => a.Datum)
                    .ThenByDescending(a => a.Start)
                    .Take(200)
                    .Select(a => new Aktivitaet
                    {
                        AktivitaetsID = a.AktivitaetsID,
                        Projektnummer = a.Projektnummer,
                        Beschreibung = a.Beschreibung,
                        Datum = a.Datum,
                        Start = a.Start,
                        Ende = a.Ende,
                        Mitarbeiter = a.Mitarbeiter,
                        Berechnen = a.Berechnen,
                        Anfahrt = a.Anfahrt,
                        MitarbeiterObj = a.MitarbeiterObj != null ? new Mitarbeiter
                        {
                            MitarbeiterNr = a.MitarbeiterObj.MitarbeiterNr,
                            Name = a.MitarbeiterObj.Name ?? "",
                            Vorname = a.MitarbeiterObj.Vorname ?? ""
                        } : null
                    })
                    .ToListAsync();

                return PartialView("_AktivitaetenListe", aktivitaeten);
            }
            catch (Exception ex)
            {
                // Return an empty list in case of an error
                return PartialView("_AktivitaetenListe", new List<Aktivitaet>());
            }
        }

        public async Task<IActionResult> GetMitarbeiterAktivitaeten(int mitarbeiterNr)
        {
            try
            {
                var aktivitaeten = await _context.Aktivitaeten
                    .Include(a => a.Projekt)
                    .Where(a => a.Mitarbeiter == mitarbeiterNr)
                    .OrderByDescending(a => a.Datum)
                    .ThenByDescending(a => a.Start)
                    .Take(10)
                    .Select(a => new Aktivitaet
                    {
                        AktivitaetsID = a.AktivitaetsID,
                        Projektnummer = a.Projektnummer,
                        Beschreibung = a.Beschreibung,
                        Datum = a.Datum,
                        Start = a.Start,
                        Ende = a.Ende,
                        Mitarbeiter = a.Mitarbeiter,
                        Berechnen = a.Berechnen,
                        Anfahrt = a.Anfahrt,
                        Projekt = a.Projekt != null ? new Projekt
                        {
                            Projektnummer = a.Projekt.Projektnummer,
                            Projektbezeichnung = a.Projekt.Projektbezeichnung ?? ""
                        } : null
                    })
                    .ToListAsync();

                return PartialView("_MitarbeiterAktivitaeten", aktivitaeten);
            }
            catch (Exception ex)
            {
                // Return an empty list in case of an error
                return PartialView("_MitarbeiterAktivitaeten", new List<Aktivitaet>());
            }
        }

        private async Task<bool> AktivitaetExists(int id)
        {
            return await _context.Aktivitaeten.AnyAsync(a => a.AktivitaetsID == id);
        }
    }
}