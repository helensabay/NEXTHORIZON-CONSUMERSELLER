using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Models;

namespace NextHorizon.Controllers
{
    [Route("api/faqs")]
    [ApiController]
    public class SupportFaqController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SupportFaqController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /api/faqs/categories (Seller only)
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.FAQs
                .Where(f => f.Status.ToLower() == "active" && f.UserType.ToLower() == "seller")
                .Select(f => f.Category)
                .Distinct()
                .ToListAsync();

            return Ok(categories);
        }

        // GET: /api/faqs/items/{category} (Seller only)
        [HttpGet("items/{category}")]
        public async Task<IActionResult> GetFaqsByCategory(string category)
        {
            var categories = await _context.FAQs
            .Where(f => f.Status.ToLower() == "active" && f.UserType.ToLower() == "seller")
            .Select(f => f.Category)
            .Distinct()
            .ToListAsync();

        var faqs = await _context.FAQs
            .Where(f => f.Status.ToLower() == "active" &&
                        f.UserType.ToLower() == "seller" &&
                        f.Category.ToLower() == category.ToLower())
            .Select(f => new 
            {
                f.FaqID,
                f.Question,
                f.Answer
            })
            .ToListAsync();

            return Ok(faqs);
        }
    }
}