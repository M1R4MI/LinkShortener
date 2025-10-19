using System.Diagnostics;
using LinkShortener.Models;
using Microsoft.AspNetCore.Mvc;
using LinkShortener.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LinkShortener.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // Show list of shortened links on the home page
        public async Task<IActionResult> Index()
        {
            var links = await _context.shortedLink.ToListAsync();
            return View(links);
        }

        // Public About page - viewable by all
        public IActionResult About()
        {
            try
            {
                var about = _context.AboutPages.OrderByDescending(a => a.LastModified).FirstOrDefault();
                if (about == null)
                {
                    about = new AboutPage { Content = "About page not configured yet.", LastModified = DateTime.Now };
                }

                ViewData["IsAdmin"] = IsAdmin();
                return View(about);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading About page.");
                return RedirectToAction(nameof(Error));
            }
        }

        // Admin-only edit POST - receives edited content from form
        [Authorize(Roles = "Administrator,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAbout(string content)
        {
            if (content == null)
                return BadRequest();

            try
            {
                var about = _context.AboutPages.OrderByDescending(a => a.LastModified).FirstOrDefault();
                if (about == null)
                {
                    about = new AboutPage();
                    _context.AboutPages.Add(about);
                }

                about.Content = content;
                about.LastModified = DateTime.UtcNow;
                about.ModifiedBy = User?.Identity?.Name ?? "Unknown";

                _context.SaveChanges();

                return RedirectToAction(nameof(About));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving About page content.");
                ModelState.AddModelError(string.Empty, "An error occurred while saving. Please try again later.");
                ViewData["IsAdmin"] = IsAdmin();
                return View("About", new AboutPage { Content = content, LastModified = DateTime.UtcNow });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            if (statusCode.HasValue)
            {
                ViewData["StatusCode"] = statusCode.Value;
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private bool IsAdmin()
        {
            return User != null && (User.IsInRole("Administrator") || User.IsInRole("Admin"));
        }
    }
}
