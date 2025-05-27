using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO; // <--- NEW: Using the ViewModel

namespace CredWise_Trail.Controllers
{
    public class CustomerController : Controller
    {
        private readonly BankLoanManagementDbContext _context;
        
        public CustomerController(BankLoanManagementDbContext context)
        {
          
            _context = context;
        }

        public IActionResult CustomerDashboard()
        {
            // You might want to consider adding authorization/authentication here too.
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        public IActionResult LoanApplication()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        public IActionResult CustomerStatements()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        public IActionResult LoanStatus()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpGet] // This action displays the KYC Upload form
        public IActionResult KYCUpload()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View(); // Return the empty form for GET request
        }
        [HttpPost] // This action handles the submission of the KYC Upload form
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KYCUpload(KycUploadViewModel model) // Model bind to the ViewModel
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            // Check if form has ID 'kycForm' to match JS. If not, ModelState will fail if form is not submitted.
            // Also ensure enctype="multipart/form-data" in the HTML form.

            if (ModelState.IsValid)
            {
                string contentRootPath = Directory.GetCurrentDirectory();
                // Define the upload directory
                string uploadFolder = Path.Combine(contentRootPath, "kyc_documents");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                try
                {
                    // Handle Identity Proof File
                    string identityFileName = null;
                    if (model.IdentityProofFile != null)
                    {
                        string fileExtension = Path.GetExtension(model.IdentityProofFile.FileName);
                        // Unique filename for Identity Proof
                        identityFileName = $"{customerId}_{Guid.NewGuid()}_identity{fileExtension}";
                        string filePath = Path.Combine(uploadFolder, identityFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.IdentityProofFile.CopyToAsync(fileStream);
                        }
                    }

                    // Create a new KycApproval entry
                    var kycApproval = new KycApproval
                    {
                        CustomerId = customerId,
                        SubmissionDate = DateTime.UtcNow,
                        Status = "Pending",
                        DocumentPath = identityFileName, // Store Identity Proof Path
                        
                    };

                    _context.KycApprovals.Add(kycApproval);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "KYC documents uploaded successfully! Your verification is pending review.";
                    return RedirectToAction("CustomerDashboard");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading KYC documents: {ex.Message}");
                    TempData["ErrorMessage"] = "An error occurred during document upload. Please try again.";
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }
            else
            {
                // Log model state errors for debugging
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"ModelState Error: {state.Key} - {error.ErrorMessage}");
                    }
                }
            }

            return View(model);
        }







        [HttpGet] // This action displays the customer details
        public async Task<IActionResult> CustomerDetails()
        {
            // Standard authentication/authorization
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            // Fetch customer from DB
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer record not found. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            // Display success message if any
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }
            if (TempData["ErrorMessage"] != null) // Also display error messages if any
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }

            return View(customer); // Pass the Customer model directly for display
        }

        [HttpGet] // This action displays the form for updating customer details
        public async Task<IActionResult> CustomerUpdate()
        {
            // Standard authentication/authorization
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer for update. Please log in again.";
                return RedirectToAction("Logout", "Account"); // Redirect to logout if ID is critical
            }

            // Fetch the existing customer record
            var customerToUpdate = await _context.Customers.FindAsync(customerId);
            if (customerToUpdate == null)
            {
                TempData["ErrorMessage"] = "Customer record not found for update. Please log in again.";
                return RedirectToAction("Logout", "Account"); // Redirect to logout if record is missing
            }

            // <--- NEW: Map domain model to ViewModel for display --->
            var viewModel = new CustomerUpdateViewModel
            {
                CustomerId = customerToUpdate.CustomerId,
                Name = customerToUpdate.Name,
                Email = customerToUpdate.Email,
                PhoneNumber = customerToUpdate.PhoneNumber,
                Address = customerToUpdate.Address
            };

            // Pass the ViewModel to the view for pre-filling the form
            return View(viewModel);
        }

        [HttpPost] // This action handles the form submission for updating customer details
        [ValidateAntiForgeryToken] // Protect against Cross-Site Request Forgery attacks
        // <--- NEW: Model bind to the ViewModel instead of the domain model --->
        public async Task<IActionResult> CustomerUpdate(CustomerUpdateViewModel viewModel)
        {
            // Standard authentication/authorization
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get logged-in CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int loggedInCustomerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer for update.";
                return RedirectToAction("Logout", "Account");
            }

            // IMPORTANT SECURITY CHECK: Ensure the CustomerId from the form matches the logged-in user's ID.
            if (viewModel.CustomerId != loggedInCustomerId) // <--- Changed from customer.CustomerId
            {
                TempData["ErrorMessage"] = "Unauthorized update attempt. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            // If the model state is valid (e.g., all [Required] fields are filled from ViewModel)
            if (ModelState.IsValid)
            {
                try
                {
                    // Fetch the existing entity from the database
                    var existingCustomer = await _context.Customers.FindAsync(viewModel.CustomerId); // <--- Changed from customer.CustomerId

                    if (existingCustomer == null)
                    {
                        TempData["ErrorMessage"] = "Customer record not found during update process.";
                        return RedirectToAction("CustomerDetails");
                    }

                    // <--- NEW: Update properties from the ViewModel to the existing domain model --->
                    existingCustomer.Name = viewModel.Name;
                    existingCustomer.Email = viewModel.Email;
                    existingCustomer.PhoneNumber = viewModel.PhoneNumber;
                    existingCustomer.Address = viewModel.Address;

                    await _context.SaveChangesAsync(); // Save changes to the database

                    TempData["SuccessMessage"] = "Profile updated successfully!"; // Set success message
                    return RedirectToAction("CustomerDetails"); // Redirect to the details page
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Log the exception
                    if (!_context.Customers.Any(e => e.CustomerId == viewModel.CustomerId)) // <--- Changed from customer.CustomerId
                    {
                        TempData["ErrorMessage"] = "The customer record you tried to update no longer exists.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "The profile you were trying to update has been modified by someone else. Please review your changes.";
                    }
                    ModelState.AddModelError("", TempData["ErrorMessage"].ToString()); // Add to model state for displaying on the form

                    // Re-fetch the current data or just return the view model to retain user input
                    // For concurrency, it's often better to re-fetch the latest from DB if possible
                    var latestCustomer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == viewModel.CustomerId);
                    // Map it back to the ViewModel if showing the latest from DB
                    var latestViewModel = new CustomerUpdateViewModel
                    {
                        CustomerId = latestCustomer?.CustomerId ?? viewModel.CustomerId,
                        Name = latestCustomer?.Name ?? viewModel.Name,
                        Email = latestCustomer?.Email ?? viewModel.Email,
                        PhoneNumber = latestCustomer?.PhoneNumber ?? viewModel.PhoneNumber,
                        Address = latestCustomer?.Address ?? viewModel.Address
                    };
                    return View(latestViewModel); // Pass the latest from DB or original submitted ViewModel
                }
                catch (Exception ex)
                {
                    // Catch any other exceptions during the save process
                    // Log the full exception details
                    ModelState.AddModelError("", "An unexpected error occurred while updating your profile. Please try again.");
                    // Return the ViewModel to retain user input
                    return View(viewModel);
                }
            }

            // If ModelState is not valid, return the view with the 'viewModel' object
            // so the form fields retain their values and validation messages are displayed.
            return View(viewModel);
        }

        public IActionResult MakePayment()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }
    }
}