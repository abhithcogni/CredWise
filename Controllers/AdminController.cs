using Microsoft.AspNetCore.Mvc;

namespace CredWise_Trail.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult AdminDashboard()
        {
            return View();
        }
        public IActionResult KYCApproval()
        {
            return View();
        }
        public IActionResult LoanApproval()
        {
            return View();
        }
        public IActionResult AddLoanProduct()
        {
            return View();
        }
        public IActionResult LoanProducts()
        {
            return View();
        }
    }
}
