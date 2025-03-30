using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;

namespace ProjektZeiterfassung.Controllers
{
    public class AuswertungController : Controller
    {
        private readonly ProjektDbContext _context;

        public AuswertungController(ProjektDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? vonDatum = null, DateTime? bisDatum = null, int? mitarbeiterId = null, int? projektId = null)
        {
            try
            {
                // Default to the current month
                if (!vonDatum.HasValue)
                {
                    vonDatum = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                }

                if (!bisDatum.HasValue)
                {
                    bisDatum = vonDatum.Value.AddMonths(1).AddDays(-1);
                }

                // Load employees for dropdown
                var mitarbeiter = await _context.Mitarbeiter
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.Vorname)
                    .ToListAsync();

                // Load projects for dropdown
                var projekte = await _context.Projekte
                    .OrderBy(p => p.Projektbezeichnung)
                    .ToListAsync();

                // Load activities with filter
                var query = _context.Aktivitaeten
                    .Include(a => a.Projekt)
                        .ThenInclude(p => p.Kunde)
                    .Include(a => a.MitarbeiterObj)
                    .Where(a => a.Datum >= vonDatum && a.Datum <= bisDatum);

                if (mitarbeiterId.HasValue)
                {
                    query = query.Where(a => a.Mitarbeiter == mitarbeiterId.Value);
                }

                if (projektId.HasValue)
                {
                    query = query.Where(a => a.Projektnummer == projektId.Value);
                }

                var aktivitaeten = await query.ToListAsync();

                // Group by employee and project for summary
                var zusammenfassung = aktivitaeten
                    .GroupBy(a => new { MitarbeiterId = a.Mitarbeiter, MitarbeiterName = $"{a.MitarbeiterObj?.Vorname ?? ""} {a.MitarbeiterObj?.Name ?? ""}" })
                    .Select(gruppe => new MitarbeiterZeitViewModel
                    {
                        MitarbeiterId = gruppe.Key.MitarbeiterId,
                        MitarbeiterName = gruppe.Key.MitarbeiterName,
                        ProjektZeiten = gruppe
                            .GroupBy(a => new { a.Projektnummer, ProjektName = a.Projekt?.Projektbezeichnung ?? "" })
                            .Select(pg => new ProjektZeitViewModel
                            {
                                ProjektId = pg.Key.Projektnummer,
                                ProjektName = pg.Key.ProjektName,
                                GesamtzeitInMinuten = pg.Sum(a => (int)((a.Ende - a.Start).TotalMinutes)),
                                BerechenbareZeitInMinuten = pg.Where(a => a.Berechnen == 1).Sum(a => (int)((a.Ende - a.Start).TotalMinutes)),
                                AnfahrtszeitInMinuten = pg.Where(a => a.Anfahrt == 1).Sum(a => (int)((a.Ende - a.Start).TotalMinutes))
                            })
                            .OrderBy(pz => pz.ProjektName)
                            .ToList(),
                        GesamtzeitInMinuten = gruppe.Sum(a => (int)((a.Ende - a.Start).TotalMinutes))
                    })
                    .OrderBy(mz => mz.MitarbeiterName)
                    .ToList();

                var viewModel = new AuswertungViewModel
                {
                    VonDatum = vonDatum.Value,
                    BisDatum = bisDatum.Value,
                    MitarbeiterId = mitarbeiterId,
                    ProjektId = projektId,
                    Mitarbeiter = mitarbeiter,
                    Projekte = projekte,
                    Aktivitaeten = aktivitaeten,
                    Zusammenfassung = zusammenfassung,
                    GesamtzeitAlleMinuten = aktivitaeten.Sum(a => (int)((a.Ende - a.Start).TotalMinutes))
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error loading evaluation data: {ex.Message}");
                return View(new AuswertungViewModel
                {
                    VonDatum = vonDatum ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                    BisDatum = bisDatum ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1),
                    Mitarbeiter = new List<Mitarbeiter>(),
                    Projekte = new List<Projekt>(),
                    Aktivitaeten = new List<Aktivitaet>(),
                    Zusammenfassung = new List<MitarbeiterZeitViewModel>()
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(DateTime vonDatum, DateTime bisDatum, int? mitarbeiterId = null, int? projektId = null)
        {
            try
            {
                // Query for activities to export
                var query = _context.Aktivitaeten
                    .Include(a => a.Projekt)
                        .ThenInclude(p => p.Kunde)
                    .Include(a => a.MitarbeiterObj)
                    .Where(a => a.Datum >= vonDatum && a.Datum <= bisDatum);

                if (mitarbeiterId.HasValue)
                {
                    query = query.Where(a => a.Mitarbeiter == mitarbeiterId.Value);
                }

                if (projektId.HasValue)
                {
                    query = query.Where(a => a.Projektnummer == projektId.Value);
                }

                var aktivitaeten = await query
                    .OrderBy(a => a.Datum)
                    .ThenBy(a => a.Start)
                    .ToListAsync();

                // Create CSV file
                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Customer number;Customer name;Project number;Project name;Date;Start;End;Description;Billable;Commute;Employee");

                foreach (var aktivitaet in aktivitaeten)
                {
                    var kunde = aktivitaet.Projekt?.Kunde;
                    var projekt = aktivitaet.Projekt;
                    var mitarbeiter = aktivitaet.MitarbeiterObj;

                    csv.AppendLine(string.Join(";",
                        kunde?.Kundennr ?? 0,
                        EscapeCsvField(kunde?.Kundenname ?? ""),
                        projekt?.Projektnummer ?? 0,
                        EscapeCsvField(projekt?.Projektbezeichnung ?? ""),
                        aktivitaet.Datum.ToString("dd.MM.yyyy"),
                        aktivitaet.Start.ToString(@"hh\:mm"),  // Corrected formatting
                        aktivitaet.Ende.ToString(@"hh\:mm"),   // Corrected formatting
                        EscapeCsvField(aktivitaet.Beschreibung ?? ""),
                        aktivitaet.Berechnen == 1 ? "Yes" : "No",
                        aktivitaet.Anfahrt == 1 ? "Yes" : "No",
                        EscapeCsvField($"{mitarbeiter?.Vorname ?? ""} {mitarbeiter?.Name ?? ""}")
                    ));
                }

                // Offer CSV file for download
                byte[] bytes = Encoding.UTF8.GetBytes(csv.ToString());
                string fileName = $"Zeiterfassung_{vonDatum:dd.MM.yyyy}_bis_{bisDatum:dd.MM.yyyy}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                // In case of errors, redirect to overview page
                TempData["ErrorMessage"] = $"Error exporting CSV file: {ex.Message}";
                return RedirectToAction(nameof(Index), new { vonDatum, bisDatum, mitarbeiterId, projektId });
            }
        }

        // Helper method to escape CSV fields
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // Quotes around fields with semicolon, line break or quotes
            if (field.Contains(";") || field.Contains("\n") || field.Contains("\"") || field.Contains(","))
            {
                // Double quotes replaced by double double quotes
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }
    }
}