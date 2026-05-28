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
            item.Quantity = Math.Max(item.Quantity, 1);

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

            var stockCheck = ValidateCartItemStock(item, variants, dbProducts);
            if (!stockCheck.IsAvailable)
            {
                return BadRequest(new { message = stockCheck.Message });
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
                    var requestedQuantity = existingItem.Quantity + item.Quantity;
                    var quantityCheck = ValidateCartItemStock(item, variants, dbProducts, requestedQuantity);
                    if (!quantityCheck.IsAvailable)
                    {
                        return null;
                    }

                    existingItem.Quantity = requestedQuantity;
                    if (item.UnitPrice > 0)
                    {
                        existingItem.UnitPrice = item.UnitPrice;
                    }
                }
                else
                {
                    cart.Add(item);
                }

                return ProductData.GetCartSnapshot();
            });

            if (cartSnapshot == null)
            {
                return BadRequest(new { message = "Only the available stock can be added to cart." });
            }

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
        public async Task<IActionResult> UpdateQuantity(int productId, [FromBody] UpdateQuantityRequest request)
        {
            var dbProducts = await _db.Products
                .AsNoTracking()
                .Where(product => product.ProductId == productId)
                .ToListAsync();
            var variants = await _db.ProductVariants
                .AsNoTracking()
                .Where(variant => variant.ProductId == productId)
                .ToListAsync();

            List<CartItem>? cartSnapshot = null;
            string? stockError = null;
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
                    var stockCheck = ValidateCartItemStock(item, variants, dbProducts, request.Quantity);
                    if (!stockCheck.IsAvailable)
                    {
                        stockError = stockCheck.Message;
                        return true;
                    }

                    item.Quantity = request.Quantity;
                }

                cartSnapshot = ProductData.GetCartSnapshot();
                return true;
            });

            if (!found)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(stockError))
            {
                return BadRequest(new { message = stockError });
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

        private static (bool IsAvailable, string Message) ValidateCartItemStock(
            CartItem cartItem,
            IReadOnlyCollection<DbProductVariant> variants,
            IReadOnlyCollection<DbProduct> dbProducts,
            int? requestedQuantity = null)
        {
            var quantity = Math.Max(requestedQuantity ?? cartItem.Quantity, 1);
            var productVariants = variants
                .Where(variant => variant.ProductId == cartItem.ProductId)
                .ToList();

            if (productVariants.Count > 0)
            {
                var matchingVariant = productVariants.FirstOrDefault(variant =>
                    (!cartItem.VariantId.HasValue || variant.Id == cartItem.VariantId.Value) &&
                    string.Equals(variant.Size, cartItem.Size, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(cartItem.Color) ||
                     string.Equals(variant.Style, cartItem.Color, StringComparison.OrdinalIgnoreCase)))
                    ?? (cartItem.VariantId.HasValue
                        ? productVariants.FirstOrDefault(variant => variant.Id == cartItem.VariantId.Value)
                        : null);

                if (matchingVariant == null)
                {
                    return (false, "Please select an available product option.");
                }

                if (matchingVariant.Quantity <= 0 ||
                    string.Equals(matchingVariant.Availability, "Out of Stock", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "This product is out of stock and cannot be ordered.");
                }

                if (quantity > matchingVariant.Quantity)
                {
                    return (false, $"Only {matchingVariant.Quantity} item(s) are available.");
                }

                return (true, string.Empty);
            }

            var fallbackProduct = ProductData.Products.FirstOrDefault(product => product.Id == cartItem.ProductId);
            if (fallbackProduct != null && fallbackProduct.Stock <= 0)
            {
                return (false, "This product is out of stock and cannot be ordered.");
            }

            if (fallbackProduct != null && quantity > fallbackProduct.Stock)
            {
                return (false, $"Only {fallbackProduct.Stock} item(s) are available.");
            }

            return dbProducts.Any(product => product.ProductId == cartItem.ProductId) || fallbackProduct != null
                ? (true, string.Empty)
                : (false, "Product not found.");
        }
    }

    public class UpdateQuantityRequest
    {
        public int Quantity { get; set; }
    }
}
