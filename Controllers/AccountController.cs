using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System; // Required for Guid

namespace CredWise_Trail.Controllers
{
    public class AccountController : Controller
    {
        private readonly BankLoanManagementDbContext _context;

        public AccountController(BankLoanManagementDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [HttpGet]
        public IActionResult LoginAdmin()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Landing()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check for existing customer email (case-insensitive comparison)
                var existingCustomer = await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower());
                if (existingCustomer)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model);
                }

                // Generate a simple unique account number
                // For production, consider a more structured and robust generation.
                string newAccountNumber = GenerateUniqueAccountNumber();

                var customer = new Customer
                {
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    AccountNumber = newAccountNumber, // Assign the generated account number
                    CreatedDate = DateTime.UtcNow // Set the registration date
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful! Please log in.";
                return RedirectToAction("Login", "Account");
            }

            return View(model);
        }

        // Helper method to generate a unique account number
        private string GenerateUniqueAccountNumber()
        {
            // A simple implementation using Guid.
            // You might want to format it, add a prefix, or ensure it's numeric for real banking.
            return "ACC-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(); // Example: ACC-F2A4B8C1D0
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Attempt to find a customer by email (case-insensitive comparison)
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());

                if (customer != null && BCrypt.Net.BCrypt.Verify(model.Password, customer.PasswordHash))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                        new Claim(ClaimTypes.Email, customer.Email),
                        new Claim(ClaimTypes.Role, "Customer"),
                        new Claim("CustomerId", customer.CustomerId.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    return RedirectToAction("CustomerDashboard", "Customer");
                }

                
                // If neither customer nor admin login succeeded, set an error message in TempData
                TempData["ErrorMessage"] = "Invalid email or password.";
            }

            // If ModelState is invalid or login failed, return the view with the model
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginAdmin(LoginAdminViewModel model)
        {
            if (ModelState.IsValid)
            {
                

                // If not a customer, attempt to find an admin by email (case-insensitive comparison)
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email.ToLower() == model.Email.ToLower());

                if (admin != null && model.PasswordHash==admin.PasswordHash)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
                        new Claim(ClaimTypes.Email, admin.Email),
                        new Claim(ClaimTypes.Role, "Admin"),
                        new Claim("AdminId", admin.AdminId.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    return RedirectToAction("AdminDashboard", "Admin");
                }

                // If neither customer nor admin login succeeded, set an error message in TempData
                TempData["ErrorMessage"] = "Invalid email or password.";
            }

            // If ModelState is invalid or login failed, return the view with the model
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Landing", "Account");
        }
    }
}