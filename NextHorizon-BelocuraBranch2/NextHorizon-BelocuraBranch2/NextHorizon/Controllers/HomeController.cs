using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using NextHorizon.Models;

namespace NextHorizon.Controllers
{
    public class HomeController : Controller
    {

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult PaymentMethods()
        {
            return View();
        }

        public IActionResult MyPurchases()
        {
            var orders = GetOrders();
            ViewBag.Orders = orders;
            var toPayCount = 0;
            var toShipCount = 0;
            var toReceiveCount = 0;
            var toReviewCount = 0;
            var returnsCount = 0;

            foreach (var order in orders)
            {
                switch (MapToTabStatus(order.Status))
                {
                    case "to-pay":
                        toPayCount++;
                        break;
                    case "to-ship":
                        toShipCount++;
                        break;
                    case "to-receive":
                        toReceiveCount++;
                        break;
                    case "to-review":
                        toReviewCount++;
                        break;
                    case "return-refund":
                        returnsCount++;
                        break;
                }
            }

            var model = new PurchaseViewModel
            {
                ToPayCount = toPayCount,
                ToShipCount = toShipCount,
                ToReceiveCount = toReceiveCount,
                ToReviewCount = toReviewCount,
                ReturnsCount = returnsCount,
                PurchaseHistory = new List<PurchaseHistory>(),
                CurrentUser = new UserInfo
                {
                    Name = "John Doe",
                    Email = "john.doe@email.com"
                }
            };

            return View(model);
        }

        public IActionResult ViewPurchaseHistory()
        {
            return View(GetOrders());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        private static List<OrderViewModel> GetOrders()
        {
            return new List<OrderViewModel>
            {
                new OrderViewModel
                {
                    ShopName = "Imong Mama Shop",
                    ProductName = "Jordan Sport Shorts",
                    Description = "Men's Dri-FIT Shorts - Black/White - Size M",
                    Price = 1995,
                    Quantity = 1,
                    Status = "Delivered",
                    ImageUrl = "https://picsum.photos/200?random=1"
                },
                new OrderViewModel
                {
                    ShopName = "Kang Nike na Shop",
                    ProductName = "Nike Sports Bra",
                    Description = "Women's Medium Support - Black - Size S",
                    Price = 2195,
                    Quantity = 2,
                    Status = "Completed",
                    ImageUrl = "https://picsum.photos/200?random=2"
                }
            };
        }

        private static string MapToTabStatus(string status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "to pay" => "to-pay",
                "to ship" => "to-ship",
                "to receive" => "to-receive",
                "delivered" => "to-receive",
                "completed" => "to-review",
                "to review" => "to-review",
                "return/refund" => "return-refund",
                _ => "other"
            };
        }
        public IActionResult Index()
        {
            return RedirectToAction("Login", "Account");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
