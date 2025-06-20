using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;  
using Microsoft.Extensions.Logging;
using CredWise_Trail.Services;
using CredWise_Trail.Filters; 

namespace CredWise_Trail.Controllers
{
    public class AccountController : Controller
    {
        private readonly BankLoanManagementDbContext _context;
        private readonly LoanUpdateOrchestratorService _loanUpdateService; 
        private readonly ILogger<AccountController> _logger;             

        public AccountController(
            BankLoanManagementDbContext context,
            LoanUpdateOrchestratorService loanUpdateService,
            ILogger<AccountController> logger)
        {
            _context = context;
            _loanUpdateService = loanUpdateService;
            _logger = logger;
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
                var existingCustomer = await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower());
                if (existingCustomer)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model);
                }

                string newAccountNumber = GenerateUniqueAccountNumber();

                var customer = new Customer
                {
                    Name = model.Name,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    AccountNumber = newAccountNumber,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful! Please log in.";
                return RedirectToAction("Login", "Account");
            }

            return View(model);
        }

        private string GenerateUniqueAccountNumber()
        {
            return "ACC-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());

                if (customer != null && BCrypt.Net.BCrypt.Verify(model.Password, customer.PasswordHash))
                {
                    _logger.LogInformation("Customer {Email} logged in successfully.", customer.Email);
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

                    // --- TRIGGER THE BATCH PROCESS ON CUSTOMER LOGIN ---
                    try
                    {
                        await _loanUpdateService.TriggerLoanStatusUpdateAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "The on-demand loan status update failed during customer login, but login process will continue.");
                    }

                    return RedirectToAction("CustomerDashboard", "Customer");
                }

                TempData["ErrorMessage"] = "Invalid email or password.";
            }

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginAdmin(LoginAdminViewModel model)
        {
            if (ModelState.IsValid)
            {
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email.ToLower() == model.Email.ToLower());

                if (admin != null && model.PasswordHash == admin.PasswordHash)
                {
                    _logger.LogInformation("Admin {Email} logged in successfully.", admin.Email);
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

                    // --- TRIGGER THE BATCH PROCESS ON ADMIN LOGIN ---
                    try
                    {
                        await _loanUpdateService.TriggerLoanStatusUpdateAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "The on-demand loan status update failed during admin login, but login process will continue.");
                    }

                    return RedirectToAction("AdminDashboard", "Admin");
                }

                TempData["ErrorMessage"] = "Invalid email or password.";
            }

            return View(model);
        }
        [HttpGet]
        [NoCache]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Landing", "Account");
        }
    }
}