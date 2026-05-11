using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private const string SharedCartCookie = "NextHorizon.SharedCart";
        private readonly AppDbContext _db;

        public CartController(AppDbContext db)
        {
            _db = db;
        }

        // POST: api/cart
        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] CartItem item)
        {
            if (item.SellerId <= 0)
            {
                item.SellerId = await _db.Products
                    .Where(p => p.ProductId == item.ProductId)
                    .Select(p => p.SellerId)
                    .FirstOrDefaultAsync();
            }

            var dbProducts = await _db.Products
                .AsNoTracking()
                .Where(product => product.ProductId == item.ProductId)
                .ToListAsync();
            var variants = await _db.ProductVariants
                .AsNoTracking()
                .Where(variant => variant.ProductId == item.ProductId)
                .ToListAsync();
            var resolvedProduct = BuildCartProduct(item, dbProducts, variants);
            if (resolvedProduct != null && resolvedProduct.Price > 0)
            {
                item.UnitPrice = resolvedProduct.Price;
            }

            var cartSnapshot = ProductData.WithCartLock(cart =>
            {
                var existingItem = cart.FirstOrDefault(ci =>
                    ci.ProductId == item.ProductId &&
                    ci.SellerId == item.SellerId &&
                    ci.VariantId == item.VariantId &&
                    string.Equals(ci.Color, item.Color, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ci.Size, item.Size, StringComparison.OrdinalIgnoreCase));

                if (existingItem != null)
                {
                    existingItem.Quantity += Math.Max(item.Quantity, 1);
                    if (item.UnitPrice > 0)
                    {
                        existingItem.UnitPrice = item.UnitPrice;
                    }
                }
                else
                {
                    item.Quantity = Math.Max(item.Quantity, 1);
                    cart.Add(item);
                }

                return ProductData.GetCartSnapshot();
            });

            SyncSharedCartCookie(cartSnapshot);
            return Ok(new { message = "Added to cart", cart = cartSnapshot });
        }

        // GET: api/cart
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var cartSnapshot = ProductData.GetCartSnapshot();

            var productIds = cartSnapshot
                .Select(ci => ci.ProductId)
                .Distinct()
                .ToList();

            var dbProducts = await _db.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.ProductId))
                .ToListAsync();

            var variants = await _db.ProductVariants
                .AsNoTracking()
                .Where(variant => productIds.Contains(variant.ProductId))
                .ToListAsync();

            var cartWithProducts = cartSnapshot
                .Select(ci => new
                {
                    CartItem = ci,
                    Product = BuildCartProduct(ci, dbProducts, variants)
                })
                .Where(x => x.Product != null)
                .ToList();

            return Ok(cartWithProducts);
        }

        // PUT: api/cart/{productId}
        [HttpPut("{productId}")]
        public IActionResult UpdateQuantity(int productId, [FromBody] UpdateQuantityRequest request)
        {
            List<CartItem>? cartSnapshot = null;
            var found = ProductData.WithCartLock(cart =>
            {
                var item = cart.FirstOrDefault(c => c.ProductId == productId);
                if (item == null)
                {
                    return false;
                }

                if (request.Quantity <= 0)
                {
                    cart.Remove(item);
                }
                else
                {
                    item.Quantity = request.Quantity;
                }

                cartSnapshot = ProductData.GetCartSnapshot();
                return true;
            });

            if (!found)
            {
                return NotFound();
            }

            SyncSharedCartCookie(cartSnapshot ?? ProductData.GetCartSnapshot());
            return request.Quantity <= 0
                ? Ok(new { message = "Removed from cart" })
                : Ok(new { message = "Quantity updated", quantity = request.Quantity });
        }

        // DELETE: api/cart/{productId}
        [HttpDelete("{productId}")]
        public IActionResult RemoveFromCart(int productId)
        {
            List<CartItem>? cartSnapshot = null;
            var removed = ProductData.WithCartLock(cart =>
            {
                var item = cart.FirstOrDefault(c => c.ProductId == productId);
                if (item == null)
                {
                    return false;
                }

                cart.Remove(item);
                cartSnapshot = ProductData.GetCartSnapshot();
                return true;
            });

            if (!removed)
            {
                return NotFound();
            }

            SyncSharedCartCookie(cartSnapshot ?? ProductData.GetCartSnapshot());
            return Ok(new { message = "Removed from cart" });
        }

        private void SyncSharedCartCookie(IReadOnlyCollection<CartItem> cartSnapshot)
        {
            Response.Cookies.Append(
                SharedCartCookie,
                JsonSerializer.Serialize(cartSnapshot),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        private Product? BuildCartProduct(CartItem cartItem, IReadOnlyCollection<DbProduct> dbProducts, IReadOnlyCollection<DbProductVariant> variants)
        {
            var dbProduct = dbProducts.FirstOrDefault(product => product.ProductId == cartItem.ProductId);
            if (dbProduct != null)
            {
                var productVariants = variants
                    .Where(variant => variant.ProductId == cartItem.ProductId)
                    .ToList();

                var matchingVariant = productVariants.FirstOrDefault(variant =>
                    (!cartItem.VariantId.HasValue || variant.Id == cartItem.VariantId.Value) &&
                    string.Equals(variant.Size, cartItem.Size, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(cartItem.Color) ||
                     string.Equals(variant.Style, cartItem.Color, StringComparison.OrdinalIgnoreCase)));

                var firstVariant = matchingVariant
                    ?? (cartItem.VariantId.HasValue
                        ? productVariants.FirstOrDefault(variant => variant.Id == cartItem.VariantId.Value)
                        : null)
                    ?? productVariants.FirstOrDefault();
                var image = firstVariant?.ImagePath ?? string.Empty;
                var price = firstVariant?.Price ?? dbProduct.Price;

                return new Product
                {
                    Id = dbProduct.ProductId,
                    Name = dbProduct.ProductName,
                    Brand = dbProduct.Brand ?? string.Empty,
                    Image = image,
                    Price = price
                };
            }

            return ProductData.Products.FirstOrDefault(product => product.Id == cartItem.ProductId);
        }
    }

    public class UpdateQuantityRequest
    {
        public int Quantity { get; set; }
    }
}
