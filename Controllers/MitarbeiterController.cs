using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektZeiterfassung.Data;
using ProjektZeiterfassung.Models;
using ProjektZeiterfassung.ViewModels;

namespace ProjektZeiterfassung.Controllers
{
    public class MitarbeiterController : Controller
    {
        private readonly ProjektDbContext _context;

        public MitarbeiterController(ProjektDbContext context)
        {
            _context = context;
        }

        // GET: Mitarbeiter
        public async Task<IActionResult> Index()
        {
            try
            {
                var mitarbeiter = await _context.Mitarbeiter
                    .OrderBy(m => m.Name)
                    .Select(m => new MitarbeiterViewModel
                    {
                        MitarbeiterNr = m.MitarbeiterNr,
                        Name = m.Name,
                        Vorname = m.Vorname
                    })
                    .ToListAsync();
                return View(mitarbeiter);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error loading employees: {ex.Message}");
                return View(new List<MitarbeiterViewModel>());
            }
        }

        // GET: Mitarbeiter/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => m.MitarbeiterNr == id)
                    .Select(m => new MitarbeiterViewModel
                    {
                        MitarbeiterNr = m.MitarbeiterNr,
                        Name = m.Name,
                        Vorname = m.Vorname
                    })
                    .FirstOrDefaultAsync();
                if (mitarbeiter == null)
                {
                    return NotFound();
                }
                return View(mitarbeiter);
            }
            catch (Exception ex)
            {
                return NotFound($"Error retrieving employee: {ex.Message}");
            }
        }

        // GET: Mitarbeiter/Create
        public IActionResult Create()
        {
            // Get the highest available employee number and suggest the next one
            int nextMitarbeiterNr = 1;
            if (_context.Mitarbeiter.Any())
            {
                nextMitarbeiterNr = _context.Mitarbeiter.Max(m => m.MitarbeiterNr) + 1;
            }
            return View(new MitarbeiterViewModel { MitarbeiterNr = nextMitarbeiterNr });
        }

        // POST: Mitarbeiter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MitarbeiterViewModel viewModel)
        {
            // Check if the employee number already exists
            if (_context.Mitarbeiter.Any(m => m.MitarbeiterNr == viewModel.MitarbeiterNr))
            {
                ModelState.AddModelError("MitarbeiterNr", "This employee number already exists.");
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var mitarbeiter = new Mitarbeiter
                    {
                        MitarbeiterNr = viewModel.MitarbeiterNr,
                        Name = viewModel.Name,
                        Vorname = viewModel.Vorname
                    };
                    _context.Add(mitarbeiter);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error creating employee: {ex.Message}");
                }
            }
            return View(viewModel);
        }

        // GET: Mitarbeiter/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            try
            {
                var mitarbeiter = await _context.Mitarbeiter
                    .Where(m => m.MitarbeiterNr == id)
                    .Select(m => new MitarbeiterViewModel
                    {
                        MitarbeiterNr = m.MitarbeiterNr,
                        Name = m.Name,
                        Vorname = m.Vorname
                    })
                    .FirstOrDefaultAsync();
                if (mitarbeiter == null)
                {
                    return NotFound();
                }
                return View(mitarbeiter);
            }
            catch (Exception ex)
            {
                return NotFound($"Error loading employee: {ex.Message}");
            }
        }

        // POST: Mitarbeiter/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MitarbeiterViewModel viewModel)
        {
            if (id != viewModel.MitarbeiterNr)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var mitarbeiter = await _context.Mitarbeiter.FindAsync(id);
                    if (mitarbeiter == null)
                    {
                        return NotFound();
                    }
                    // Employee number cannot be changed, so we only take name and first name
                    mitarbeiter.Name = viewModel.Name;
                    mitarbeiter.Vorname = viewModel.Vorname;
                    _context.Update(mitarbeiter);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MitarbeiterExists(viewModel.MitarbeiterNr))
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
                    ModelState.AddModelError(string.Empty, $"Error updating employee: {ex.Message}");
                }
            }
            return View(viewModel);
        }

        // Helper method to check if an employee exists
        private bool MitarbeiterExists(int id)
        {
            return _context.Mitarbeiter.Any(e => e.MitarbeiterNr == id);
        }
    }
}