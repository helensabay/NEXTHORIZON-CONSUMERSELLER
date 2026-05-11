using Microsoft.AspNetCore.Mvc;

namespace NextHorizon.Controllers
{
    public class FitnessOverviewController : Controller
    {
        public IActionResult DailyFitnessOverview()
        {
            return View();
        }

        public IActionResult WeeklyFitnessOverview()
        {
            return View();
        }

        public IActionResult MonthlyFitnessOverview()
        {
            return View();
        }

        public IActionResult YearlyFitnessOverview()
        {
            return View();
        }
    }
}
