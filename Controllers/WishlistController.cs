using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Data;
using MyAspNetApp.Models;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistController : ControllerBase
    {
        // POST: api/wishlist/{productId}
        [HttpPost("{productId}")]
        public IActionResult AddToWishlist(int productId)
        {
            if (!ProductData.Wishlist.Any(w => w.ProductId == productId))
            {
                ProductData.Wishlist.Add(new WishlistItem { ProductId = productId });
                return Ok(new { message = "Added to wishlist" });
            }
            return Ok(new { message = "Already in wishlist" });
        }

        // GET: api/wishlist
        [HttpGet]
        public IActionResult GetWishlist()
        {
            var wishlistWithProducts = ProductData.Wishlist.Select(wi => new
            {
                WishlistItem = wi,
                Product = ProductData.Products.FirstOrDefault(p => p.Id == wi.ProductId)
            }).ToList();
            return Ok(wishlistWithProducts);
        }

        // DELETE: api/wishlist/{productId}
        [HttpDelete("{productId}")]
        public IActionResult RemoveFromWishlist(int productId)
        {
            var item = ProductData.Wishlist.FirstOrDefault(w => w.ProductId == productId);
            if (item != null)
            {
                ProductData.Wishlist.Remove(item);
                return Ok(new { message = "Removed from wishlist" });
            }
            return NotFound();
        }
    }
}
