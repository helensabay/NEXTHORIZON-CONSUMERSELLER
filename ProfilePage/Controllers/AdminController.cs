using Microsoft.AspNetCore.Mvc;
using NextHorizon.Services.AdminServices; // Your service namespace
using NextHorizon.Models.Admin_Models;    // Your model namespace

namespace NextHorizon.Controllers
{
    public class AdminController : Controller
    {
        private readonly DashboardService _dashboardService = new DashboardService();

        // URL: /Admin/Dashboard
        public IActionResult Dashboard()
        {
            var model = _dashboardService.GetHeroStats();
            return View(model);
        }

        public IActionResult Analytics()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult Sellers()
        {
            return View();
        }

        public IActionResult Logistics()
        {
            return View();
        }
        public IActionResult Tasks()
        {
            return View();
        }

        public IActionResult ChallengeDetails()
        {
            return View();
        }

        public IActionResult Moderation()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        public IActionResult Notifications()
        {
            return View();
        }


        public IActionResult Logout()
        {

            //modify later to log out the user and redirect to login page
            return View();
        }
    }
}