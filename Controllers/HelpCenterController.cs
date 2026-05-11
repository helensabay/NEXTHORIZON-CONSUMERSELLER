using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Models.HelpCenter;

namespace MyAspNetApp.Controllers
{
    [Route("Help")]
    public sealed class HelpCenterController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("Assistant")]
        public IActionResult Assistant()
        {
            ViewData["Title"] = "Help Assistant";
            return View();
        }

        [HttpGet("{slug}")]
        public IActionResult Topic(string slug)
        {
            var normalizedSlug = slug?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedSlug))
            {
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(normalizedSlug, "contact", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["Title"] = "Contact Support";
                return View("Contact", new HelpTopicPageViewModel(normalizedSlug, IsContactPage: true));
            }

            ViewData["Title"] = "Help Center Topic";
            return View("Topic", new HelpTopicPageViewModel(normalizedSlug));
        }
    }
}
