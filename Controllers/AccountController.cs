using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net; // Make sure this is imported

namespace CredWise_Trail.Controllers
{
    public class AccountController : Controller
    {
        private readonly BankLoanManagementDbContext _context; // Verify this DbContext name

        public AccountController(BankLoanManagementDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Account/Register
        public IActionResult Register() // Corresponds to your Register.cshtml
        {
            return View();
        }

        // GET: /Account/Landing (assuming this is a public landing page)
        public IActionResult Landing()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegistrationViewModel model) // Action for POST /Account/Register
        {
            if (ModelState.IsValid)
            {
                var existingCustomer = await _context.Customers.AnyAsync(c => c.Email == model.Email);
                if (existingCustomer)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model); // Return the view with the error so user can fix
                }

                var customer = new Customer
                {
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address // Make sure Address is mapped
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful! Please log in.";
                return RedirectToAction("Login", "Account"); // Redirect to Login page
            }

            // If ModelState is NOT valid, return the view with the model to display errors.
            return View(model); // IMPORTANT: This was changed from RedirectToAction("Landing","Account")
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == model.Email);

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

                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == model.Email);

                if (admin != null && BCrypt.Net.BCrypt.Verify(model.Password, admin.PasswordHash))
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

                ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
            }

            // If ModelState is NOT valid or login failed, return the view with the model to display errors.
            // This was changed from RedirectToAction("Landing","Account")
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Landing", "Account"); // Redirect to the landing page after logout
        }
    }
}