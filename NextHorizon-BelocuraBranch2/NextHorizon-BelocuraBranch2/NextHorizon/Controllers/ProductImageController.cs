using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;

namespace NextHorizon.Controllers;

public class ProductImageController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public ProductImageController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    // Serves binary image for a variant: <img src="/ProductImage/Variant/42" />
    [HttpGet("/ProductImage/Variant/{variantId:int}")]
    public async Task<IActionResult> Variant(int variantId)
    {
        var variant = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new { v.ImageData, v.ImageMimeType, v.ImagePath })
            .FirstOrDefaultAsync();

        if (variant == null) return NotFound();

        if (variant.ImageData != null && variant.ImageData.Length > 0)
        {
            var mime = string.IsNullOrEmpty(variant.ImageMimeType) ? "image/jpeg" : variant.ImageMimeType;
            return File(variant.ImageData, mime);
        }

        if (!string.IsNullOrEmpty(variant.ImagePath))
            return Redirect(variant.ImagePath);

        return NotFound();
    }

    // Returns every stored image for a product and backfills missing VARBINARY data from disk when available.
    [HttpGet("/ProductImage/Product/{productId:int}")]
    public async Task<IActionResult> Product(int productId)
    {
        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.Id)
            .ToListAsync();

        if (variants.Count == 0)
            return NotFound(new { message = "Product has no variants or images.", productId });

        var changed = false;
        var images = new List<object>();

        foreach (var variant in variants)
        {
            if (!HasUsableImage(variant.ImageData, variant.ImagePath))
                continue;

            if (await TryPopulateVariantImageDataAsync(variant))
                changed = true;

            images.Add(new
            {
                variantId = variant.Id,
                variant.ProductId,
                variant.Style,
                variant.Size,
                imageUrl = Url.Action(nameof(Variant), "ProductImage", new { variantId = variant.Id }) ?? $"/ProductImage/Variant/{variant.Id}",
                imagePath = variant.ImagePath,
                mimeType = variant.ImageMimeType,
                imageData = variant.ImageData,
                byteLength = variant.ImageData?.Length ?? 0,
                hasBinaryData = variant.ImageData is { Length: > 0 }
            });
        }

        if (changed)
            await _db.SaveChangesAsync();

        return Ok(new
        {
            productId,
            imageCount = images.Count,
            images
        });
    }

    // ONE-TIME MIGRATION: Converts existing file-path images to VARBINARY for a seller's variants
    // Call once via: GET /ProductImage/MigrateSellerImages/5
    // Remove or restrict this endpoint after migration is complete
    [HttpGet("/ProductImage/MigrateSellerImages/{sellerId:int}")]
    public async Task<IActionResult> MigrateSellerImages(int sellerId)
    {
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.Product!.SellerId == sellerId
                && v.ImageData == null
                && !string.IsNullOrEmpty(v.ImagePath))
            .ToListAsync();

        int converted = 0, skipped = 0;
        var results = new List<string>();

        foreach (var variant in variants)
        {
            if (!await TryPopulateVariantImageDataAsync(variant))
            {
                results.Add($"VariantId {variant.Id}: SKIPPED - file not found");
                skipped++;
                continue;
            }

            results.Add($"VariantId {variant.Id}: OK - {variant.ImageData!.Length} bytes ({variant.ImageMimeType})");
            converted++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Migration complete. Converted: {converted}, Skipped: {skipped}",
            details = results
        });
    }

    private async Task<bool> TryPopulateVariantImageDataAsync(NextHorizon.Models.DbProductVariant variant)
    {
        if (variant.ImageData is { Length: > 0 } || string.IsNullOrWhiteSpace(variant.ImagePath))
            return false;

        var physicalPath = ResolvePhysicalPath(variant.ImagePath);
        if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
            return false;

        variant.ImageData = await System.IO.File.ReadAllBytesAsync(physicalPath);
        variant.ImageMimeType = NormalizeMimeType(Path.GetExtension(physicalPath));
        return true;
    }

    private string? ResolvePhysicalPath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        var relativePath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.WebRootPath, relativePath);
    }

    private static bool HasUsableImage(byte[]? imageData, string? imagePath)
    {
        return imageData is { Length: > 0 } || !string.IsNullOrWhiteSpace(imagePath);
    }

    private static string NormalizeMimeType(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}
