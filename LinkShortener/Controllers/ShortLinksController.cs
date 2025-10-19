using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LinkShortener.Data;
using LinkShortener.Models;
using Microsoft.AspNetCore.Authorization;

namespace LinkShortener.Controllers
{
    public class ShortLinksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShortLinksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ShortLinks
        public async Task<IActionResult> Index()
        {
            return View(await _context.shortedLink.ToListAsync());
        }

        // GET: ShortLinks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shortlink = await _context.shortedLink
                .FirstOrDefaultAsync(m => m.ID == id);
            if (shortlink == null)
            {
                return NotFound();
            }

            return View(shortlink);
        }

        // GET: ShortLinks/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: ShortLinks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OriginalURL")] Shortlink shortlink)
        {
            var userName = User?.Identity?.Name ?? "Unknown";
            var shortCode = GenerateShortCode(shortlink.OriginalURL);
            var newLink = new Shortlink
            {
                OriginalURL = shortlink.OriginalURL,
                ShortURL = shortCode,
                CreatedBy = userName,
                CreatedDate = DateTime.Now,
                RedirectCount = 0
            };

            shortlink = newLink;

            // Validate the fully populated model (including ShortURL, CreatedBy, etc.)
            ModelState.Clear();
            if (TryValidateModel(newLink))
            {
                _context.Add(newLink);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(newLink);
        }

        // GET: ShortLinks/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shortlink = await _context.shortedLink.FindAsync(id);
            if (shortlink == null)
            {
                return NotFound();
            }
            return View(shortlink);
        }

        // POST: ShortLinks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,OriginalURL,ShortURL,CreatedDate,CreatedBy,RedirectCount")] Shortlink shortlink)
        {
            if (id != shortlink.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shortlink);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShortlinkExists(shortlink.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(shortlink);
        }

        // GET: ShortLinks/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shortlink = await _context.shortedLink
                .FirstOrDefaultAsync(m => m.ID == id);
            if (shortlink == null)
            {
                return NotFound();
            }

            return View(shortlink);
        }

        // POST: ShortLinks/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shortlink = await _context.shortedLink.FindAsync(id);
            if (shortlink != null)
            {
                _context.shortedLink.Remove(shortlink);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ShortlinkExists(int id)
        {
            return _context.shortedLink.Any(e => e.ID == id);
        }


        //[HttpGet("{shortCode}")]
        //public IActionResult RedirectToOriginal(string shortCode)
        //{
        //    var link = _context.shortedLink.FirstOrDefault(x => x.ShortURL == shortCode);
        //    if (link == null)
        //        return NotFound("Short URL not found");

        //    link.RedirectCount++;
        //    _context.SaveChanges();

        //    return Redirect(link.OriginalURL);
        //}

        private string GenerateShortCode(string? input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input + DateTime.UtcNow));
            var base64 = Convert.ToBase64String(hashBytes)
                .Replace("/", "")
                .Replace("+", "")
                .Replace("=", "")
                .Substring(0, 8);

            var result = $"short.com//{base64}";
            return result;
        }
    }
}
