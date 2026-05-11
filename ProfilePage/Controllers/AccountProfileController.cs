using Microsoft.AspNetCore.Mvc;
using NextHorizon.Services; 
using NextHorizon.Models;

namespace NextHorizon.Controllers
{
    public class AccountProfileController : Controller
    {
        private readonly OrderService _orderService;

        // The system automatically provides the service here
        public AccountProfileController(OrderService orderService)
        {
            _orderService = orderService;
        }

        public IActionResult ProfileView()
        {
            return View();
        }

        public IActionResult UpdateProfile()
        {
            return View();
        }

        public IActionResult UploadActivity()
        {
            return View();
        }

        public IActionResult ShippingAddress()
        {
            return View();
        }

        public IActionResult PaymentMethods()
        {
            return View();
        }


        public IActionResult MyPurchases()
        {
            // Get the list from your new Service
            var orders = _orderService.GetUserPurchases();

            // Send that list to the MyPurchases.cshtml view
            return View(orders);
        }
    }
}
