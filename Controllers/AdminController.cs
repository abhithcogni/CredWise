using Microsoft.AspNetCore.Mvc;

namespace CredWise_Trail.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
