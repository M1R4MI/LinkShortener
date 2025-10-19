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
using System.Text;
using System.Security.Cryptography;

namespace LinkShortener.Controllers
{
    public class ShortLinksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string DomainPrefix = "short.com/"; // display prefix
        private const int MaxTotalShortUrlLength = 20; // must match model MaxLength

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

        // GET: ShortLinks/RedirectToOriginal?shortUrl={shortUrl}
        [HttpGet]
        public async Task<IActionResult> RedirectToOriginal(string shortUrl)
        {
            if (string.IsNullOrEmpty(shortUrl))
            {
                return NotFound();
            }

            // Accept either a full short URL (e.g. "short.com/abc") or just the code ("abc").
            var code = shortUrl.Contains('/') ? shortUrl.Substring(shortUrl.LastIndexOf('/') + 1) : shortUrl;

            var link = await _context.shortedLink.FirstOrDefaultAsync(x => x.ShortURL == code);
            if (link == null)
            {
                return NotFound();
            }

            link.RedirectCount++;
            await _context.SaveChangesAsync();

            // Ensure the original URL is absolute (has scheme) before redirecting
            var target = NormalizeUrl(link.OriginalURL);

            return Redirect(target);
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

            // Normalize the OriginalURL to include a scheme if the user omitted it (e.g., "youtube.com" -> "https://youtube.com")
            shortlink.OriginalURL = NormalizeUrl(shortlink.OriginalURL);

            var shortCode = await GenerateShortCodeAsync(shortlink.OriginalURL);
            var newLink = new Shortlink
            {
                OriginalURL = shortlink.OriginalURL,
                // Store only the generated code (without domain) so it always fits the MaxLength constraint
                ShortURL = shortCode,
                CreatedBy = userName,
                CreatedDate = DateTime.Now,
                RedirectCount = 0
            };

            // Validate the fully populated model (including ShortURL, CreatedBy, etc.)
            ModelState.Clear();
            if (TryValidateModel(newLink))
            {
                _context.Add(newLink);
                await _context.SaveChangesAsync();
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

            var currentUser = User?.Identity?.Name ?? string.Empty;
            if (!IsAdmin() && !string.Equals(shortlink.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
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
            if (shortlink == null)
                return NotFound();

            var currentUser = User?.Identity?.Name ?? string.Empty;
            if (!IsAdmin() || !string.Equals(shortlink.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            _context.shortedLink.Remove(shortlink);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Ensures the URL has an HTTP/HTTPS scheme. If missing, prepends "https://".
        private string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url ?? string.Empty;

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return "https://" + url;
        }

        // Returns true if the current user is in an administrator role. The project previously had a typo in the role name, so check both variants.
        private bool IsAdmin()
        {
            return User != null && (User.IsInRole("Administrator") || User.IsInRole("Admin"));
        }

        // Generate a short code using SHA256, make it URL-safe, and ensure uniqueness by checking the database.
        private async Task<string> GenerateShortCodeAsync(string? input)
        {
            // Use a fallback input if null/empty
            var baseInput = string.IsNullOrEmpty(input) ? Guid.NewGuid().ToString() : input;

            const int maxAttempts = 1000;
            int attempt = 0;

            // Determine maximum code length so that DomainPrefix + code fits into MaxTotalShortUrlLength
            var maxCodeLength = Math.Max(4, MaxTotalShortUrlLength - DomainPrefix.Length); // ensure at least 4

            while (attempt < maxAttempts)
            {
                // Combine input with attempt counter and ticks to vary the hash
                var combined = $"{baseInput}|{attempt}|{DateTime.UtcNow.Ticks}";

                byte[] hashBytes;
                using (var sha256 = SHA256.Create())
                {
                    hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                }

                // Convert to URL-safe Base64-like string (remove +, /, =)
                var candidateKey = Convert.ToBase64String(hashBytes)
                    .Replace("+", "")
                    .Replace("/", "")
                    .Replace("=", "");

                // Keep only alphanumeric characters to be safe for URLs
                candidateKey = new string(candidateKey.Where(char.IsLetterOrDigit).ToArray());
                if (candidateKey.Length < maxCodeLength)
                {
                    candidateKey = candidateKey.PadRight(maxCodeLength, 'A');
                }

                // Limit to maxCodeLength so when prefixed it does not exceed MaxTotalShortUrlLength
                candidateKey = candidateKey.Substring(0, Math.Min(maxCodeLength, candidateKey.Length));

                var candidate = candidateKey; // store only code, not domain

                // Check database for existing short URL code
                var exists = await _context.shortedLink.AnyAsync(s => s.ShortURL == candidate);
                if (!exists)
                {
                    return candidate;
                }

                attempt++;
            }

            throw new InvalidOperationException("Unable to generate a unique short code after multiple attempts.");
        }
    }
}
