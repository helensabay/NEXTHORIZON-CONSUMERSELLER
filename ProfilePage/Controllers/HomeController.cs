using Microsoft.AspNetCore.Mvc;
using NextHorizon.Models;
using NextHorizon.Services.AdminServices;
using System.Diagnostics;

namespace NextHorizon.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Redirects the user to /Admin/Dashboard  for testing
            return RedirectToAction("Dashboard", "Admin");


            //for consumer UI
            //return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}