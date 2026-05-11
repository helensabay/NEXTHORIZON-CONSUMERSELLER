using Microsoft.AspNetCore.Mvc;

namespace MyAspNetApp.Controllers
{
    // Route-compatible wrapper for /Product/* URLs used by seller pages.
    public class ProductController : Controller
    {
        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("CreateProduct", "Seller");
        }

        [HttpGet]
        public IActionResult ViewProduct(int? id, string? state)
        {
            return RedirectToAction("ViewProduct", "Seller", new { id, state });
        }

        [HttpGet]
        public IActionResult SizeGuide()
        {
            return RedirectToAction("SizeGuide", "Seller");
        }

        [HttpGet]
        public IActionResult ViewRatings(int id)
        {
            return RedirectToAction("ViewRatings", "Seller", new { id });
        }

        [HttpGet]
        public IActionResult ViewSizeGuide(int id)
        {
            return RedirectToAction("ViewSizeGuide", "Seller", new { id });
        }
    }
}
