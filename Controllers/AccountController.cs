using Microsoft.AspNetCore.Mvc;

namespace CredWise_Trail.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
        public IActionResult Register()
        {
            return View();
        }
        public IActionResult Landing()
        {
            return View();
        }
    }
}
