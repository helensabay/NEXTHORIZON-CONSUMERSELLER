using Microsoft.AspNetCore.Mvc;
using NextHorizon.Data;
using NextHorizon.Models;
using System.Linq;

namespace NextHorizon.Controllers
{
    public class HelpController : Controller
    {
        private readonly AppDbContext _context;

        public HelpController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Help()
        {
            return View();
        }

        public IActionResult Orders()
        {
            var faqs = _context.FAQs
                .Where(f => f.UserType.ToLower() == "seller" && 
                           f.Category.ToLower() == "testing" && 
                           f.Status.ToLower() == "active")
                .OrderBy(f => f.DateAdded)
                .ToList();
            return View(faqs);
        }

        public IActionResult Payouts()
        {
            var faqs = _context.FAQs
                .Where(f => f.UserType.ToLower() == "seller" && 
                           f.Category.ToLower() == "testing" && 
                           f.Status.ToLower() == "active")
                .OrderBy(f => f.DateAdded)
                .ToList();
            return View(faqs);
        }

        public IActionResult Account()
        {
            var faqs = _context.FAQs
                .Where(f => f.UserType.ToLower() == "seller" && 
                           f.Category.ToLower() == "testing" && 
                           f.Status.ToLower() == "active")
                .OrderBy(f => f.DateAdded)
                .ToList();
            return View(faqs);
        }

        public IActionResult Shipping()
        {
            var faqs = _context.FAQs
                .Where(f => f.UserType.ToLower() == "seller" && 
                           f.Category.ToLower() == "testing" && 
                           f.Status.ToLower() == "active")
                .OrderBy(f => f.DateAdded)
                .ToList();
            return View(faqs);
        }

        public IActionResult Marketing()
        {
            var faqs = _context.FAQs
                .Where(f => f.UserType.ToLower() == "seller" && 
                           f.Category.ToLower() == "baligya" && 
                           f.Status.ToLower() == "active")
                .OrderBy(f => f.DateAdded)
                .ToList();
            return View(faqs);
        }
    }
}