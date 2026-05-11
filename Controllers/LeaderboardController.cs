using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Services;

namespace MyAspNetApp.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly LeaderboardService _leaderboardService;

        public LeaderboardController(LeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpGet]
        public async Task<IActionResult> LeaderboardDashboard(CancellationToken cancellationToken)
        {
            var model = await _leaderboardService.GetLeaderboardAsync(20, cancellationToken);
            return View(model);
        }

        [HttpGet("/Leaderboard/CoverImage/{uploadId:int}")]
        public async Task<IActionResult> CoverImage(int uploadId, CancellationToken cancellationToken)
        {
            var image = await _leaderboardService.GetCoverImageAsync(uploadId, cancellationToken);
            if (image?.Bytes?.Length > 0)
            {
                return File(image.Bytes, image.ContentType ?? "application/octet-stream");
            }

            if (!string.IsNullOrWhiteSpace(image?.LocalFilePath))
            {
                return PhysicalFile(image.LocalFilePath, image.ContentType ?? "application/octet-stream");
            }

            if (!string.IsNullOrWhiteSpace(image?.RedirectUrl))
            {
                return Redirect(image.RedirectUrl);
            }

            return NotFound();
        }

        [HttpGet("/Leaderboard/ProfileImage/{userId:int}")]
        public async Task<IActionResult> ProfileImage(int userId, CancellationToken cancellationToken)
        {
            var image = await _leaderboardService.GetProfileImageAsync(userId, cancellationToken);
            if (image?.Bytes?.Length > 0)
            {
                Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";
                return File(image.Bytes, image.ContentType ?? "application/octet-stream");
            }

            return NotFound();
        }
    }
}
