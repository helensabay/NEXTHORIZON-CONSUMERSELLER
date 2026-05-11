using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ReviewsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET: api/products/{productId}/reviews
        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var reviews = await _db.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            var reviewIds = reviews.Select(r => r.Id).ToList();
            var reviewImages = await _db.ReviewImages
                .Where(i => reviewIds.Contains(i.ReviewId))
                .ToListAsync();

            var payload = reviews.Select(r => new Review
            {
                Id = r.Id,
                UserName = r.UserName,
                Rating = (double)r.Rating,
                ShortReview = r.ShortReview,
                Comment = r.Comment,
                Date = r.Date,
                VerifiedPurchase = r.VerifiedPurchase,
                Email = r.Email,
                Recommend = r.Recommend,
                Comfort = r.Comfort,
                Quality = r.Quality,
                SizeFit = r.SizeFit,
                WidthFit = r.WidthFit,
                SellerReply = r.SellerReply,
                SellerReplyDate = r.SellerReplyDate,
                ProductId = r.ProductId,
                Images = reviewImages
                    .Where(i => i.ReviewId == r.Id)
                    .Select(i => i.ImageUrl)
                    .ToList()
            }).ToList();

            return Ok(payload);
        }

        // POST: api/products/{productId}/reviews
        [HttpPost]
        public async Task<IActionResult> AddReview(int productId, [FromBody] CreateReviewRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Invalid review payload." });

            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "UserName and Email are required." });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5." });

            var productExists = await _db.Products.AnyAsync(p => p.ProductId == productId);
            if (!productExists)
                return NotFound(new { message = "Product not found." });

            var dbReview = new DbReview
            {
                UserName = request.UserName.Trim(),
                Rating = (decimal)request.Rating,
                ShortReview = request.ShortReview?.Trim() ?? string.Empty,
                Comment = request.Comment?.Trim() ?? string.Empty,
                Date = DateTime.Now,
                VerifiedPurchase = request.VerifiedPurchase ?? true,
                Email = request.Email.Trim(),
                Recommend = request.Recommend ?? false,
                Comfort = request.Comfort,
                Quality = request.Quality,
                SizeFit = request.SizeFit,
                WidthFit = request.WidthFit,
                ProductId = productId
            };

            _db.Reviews.Add(dbReview);
            await _db.SaveChangesAsync();

            if (request.Images != null && request.Images.Count > 0)
            {
                var savedImageUrls = await SaveReviewImagesAsync(dbReview.Id, request.Images);
                foreach (var imageUrl in savedImageUrls)
                {
                    _db.ReviewImages.Add(new DbReviewImage
                    {
                        ReviewId = dbReview.Id,
                        ImageUrl = imageUrl
                    });
                }

                if (savedImageUrls.Count > 0)
                    await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Review added successfully.", reviewId = dbReview.Id });
        }

        // POST: api/products/{productId}/reviews/{reviewId}/reply
        [HttpPost("{reviewId}/reply")]
        public async Task<IActionResult> SaveSellerReply(int productId, int reviewId, [FromBody] SellerReplyRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Reply))
            {
                return BadRequest(new { message = "Reply is required." });
            }

            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId);
            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            review.SellerReply = request.Reply.Trim();
            review.SellerReplyDate = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Reply saved successfully.",
                sellerReply = review.SellerReply,
                sellerReplyDate = review.SellerReplyDate
            });
        }

        private async Task<List<string>> SaveReviewImagesAsync(int reviewId, List<string> rawImages)
        {
            var savedUrls = new List<string>();
            if (rawImages.Count == 0)
                return savedUrls;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                return savedUrls;

            var reviewUploadDir = Path.Combine(webRoot, "uploads", "reviews");
            Directory.CreateDirectory(reviewUploadDir);

            foreach (var rawImage in rawImages.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!rawImage.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    savedUrls.Add(rawImage);
                    continue;
                }

                var commaIndex = rawImage.IndexOf(',');
                if (commaIndex <= 0 || commaIndex >= rawImage.Length - 1)
                    continue;

                var metadata = rawImage.Substring(0, commaIndex);
                var base64Part = rawImage.Substring(commaIndex + 1);

                string extension;
                if (metadata.Contains("image/png", StringComparison.OrdinalIgnoreCase))
                    extension = ".png";
                else if (metadata.Contains("image/webp", StringComparison.OrdinalIgnoreCase))
                    extension = ".webp";
                else
                    extension = ".jpg";

                try
                {
                    var bytes = Convert.FromBase64String(base64Part);
                    var fileName = $"review-{reviewId}-{Guid.NewGuid():N}{extension}";
                    var filePath = Path.Combine(reviewUploadDir, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                    savedUrls.Add($"/uploads/reviews/{fileName}");
                }
                catch
                {
                    // Ignore malformed image payloads and continue processing others.
                }
            }

            return savedUrls;
        }

        public class CreateReviewRequest
        {
            public string UserName { get; set; } = string.Empty;
            public double Rating { get; set; }
            public string? ShortReview { get; set; }
            public string? Comment { get; set; }
            public bool? VerifiedPurchase { get; set; }
            public string Email { get; set; } = string.Empty;
            public bool? Recommend { get; set; }
            public int? Comfort { get; set; }
            public int? Quality { get; set; }
            public int? SizeFit { get; set; }
            public int? WidthFit { get; set; }
            public List<string>? Images { get; set; }
        }

        public class SellerReplyRequest
        {
            public string Reply { get; set; } = string.Empty;
        }
    }
}
