using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Services;
using MyAspNetApp.Models;

namespace MyAspNetApp.Controllers
{
    public class OrderController : Controller
    {
        private const string SharedUserIdCookie = "NextHorizon.SharedUserId";

        private readonly AppDbContext _dbContext;
        private readonly OrderService _orderService;

        public OrderController(AppDbContext dbContext, OrderService orderService)
        {
            _dbContext = dbContext;
            _orderService = orderService;
        }

        public async Task<IActionResult> MyPurchasesOptions(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(MyPurchasesOptions), "Order") });
            }

            var consumerId = await _dbContext.Consumers
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Select(x => (int?)x.ConsumerId)
                .FirstOrDefaultAsync(cancellationToken);

            var orders = await _orderService.GetUserPurchasesAsync(userId, consumerId, cancellationToken);
            return View(orders);
        }

        private int? GetCurrentUserId()
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId.HasValue)
            {
                return sessionUserId.Value;
            }

            if (int.TryParse(Request.Cookies[SharedUserIdCookie], out var cookieUserId))
            {
                return cookieUserId;
            }

            return null;
        }
    }
}
