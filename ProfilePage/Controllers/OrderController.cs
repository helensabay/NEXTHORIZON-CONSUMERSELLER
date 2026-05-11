using Microsoft.AspNetCore.Mvc;
using NextHorizon.Services; // Ensure this matches your service namespace
using NextHorizon.Models;

namespace NextHorizon.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderService _orderService;

        public OrderController()
        {
            _orderService = new OrderService();
        }

        public IActionResult MyPurchasesOptions()
        {
            // 1. Get the full list from your Service exactly like MyPurchases
            var orders = _orderService.GetUserPurchases();

            // 2. Send that list (List<OrderViewModel>) to the view
            return View(orders);
        }
    }
}