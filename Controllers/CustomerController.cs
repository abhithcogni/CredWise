using Microsoft.AspNetCore.Mvc;

namespace CredWise_Trail.Controllers
{
    public class CustomerController : Controller
    {
        public IActionResult CustomerDashboard()
        {
            return View();
        }
        public IActionResult LoanApplication()
        {
            return View();
        }
        public IActionResult CustomerStatements()
        {
            return View();
        }
        public IActionResult LoanStatus()
        {
            return View();
        }
        public IActionResult KYCUpload()
        {
            return View();
        }
        public IActionResult CustomerDetails()
        {
            return View();
        }
        public IActionResult CustomerUpdate()
        {
            return View();
        }
        public IActionResult MakePayment()
        {
            return View();
        }
    }
}
