using CredWise_Trail.Models;
using CredWise_Trail.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization; // Add this using directive for CultureInfo

namespace CredWise_Trail.Controllers
{
    public class AdminController : Controller
    {
        private readonly BankLoanManagementDbContext _context;
        private readonly ILogger<AdminController> _logger; // Added for logging

        public AdminController(BankLoanManagementDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }// Initialize logger



        public IActionResult AdminDashboard()
        {
            return View();
        }

        public async Task<IActionResult> KycApproval()
        {
            try
            {
                // Fetch KYC data from the database, including the related Customer information
                // Ensure you have a 'Customers' DbSet in YourDbContext or similar
                var kycApprovals = await _context.KycApprovals
                                                 .Include(k => k.Customer) // Eager load Customer data
                                                 .ToListAsync();

                // Pass the data to the view
                return View(kycApprovals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching KYC approvals for display.");
                // Optionally return an error view or an empty list
                return View(new List<KycApproval>());
            }
        }
        [HttpPost]
        public async Task<IActionResult> UpdateKycStatus(int kycId, string status)
        {
            _logger.LogInformation($"Attempting to update KYC ID: {kycId} from frontend with status: '{status}'.");
            try
            {
                var kycApproval = await _context.KycApprovals.FindAsync(kycId);

                if (kycApproval == null)
                {
                    _logger.LogWarning($"KYC record with ID {kycId} not found for update.");
                    return Json(new { success = false, message = "KYC record not found." });
                }

                // --- FIX STARTS HERE ---
                // Normalize the incoming status string to TitleCase (e.g., "pending" -> "Pending")
                // This ensures it matches the expected casing in your database and validation.
                string normalizedStatus = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(status.ToLowerInvariant());

                _logger.LogInformation($"Normalized status for validation: '{normalizedStatus}'.");

                // Validate the normalized status against the expected PascalCase values
                if (normalizedStatus != "Pending" && normalizedStatus != "Approved" && normalizedStatus != "Rejected")
                {
                    _logger.LogWarning($"Invalid status '{status}' (normalized to '{normalizedStatus}') provided for KYC ID {kycId}.");
                    return Json(new { success = false, message = "Invalid status provided." });
                }

                // Update the status
                kycApproval.Status = normalizedStatus; // Assign the normalized (PascalCase) status

                // Update the ApprovalDate based on the normalized status
                if (normalizedStatus == "Approved" || normalizedStatus == "Rejected")
                {
                    kycApproval.ApprovalDate = DateTime.Now;
                }
                else // if status reverts to Pending
                {
                    kycApproval.ApprovalDate = null;
                }
                // --- FIX ENDS HERE ---

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully updated KYC ID: {kycId} to status: {kycApproval.Status}.");

                return Json(new { success = true });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, $"Concurrency error updating KYC ID {kycId}.");
                return Json(new { success = false, message = "Concurrency conflict. Data was modified by another user." });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Database update error for KYC ID {kycId}. Details: {ex.InnerException?.Message ?? ex.Message}"); // Log inner exception for more detail
                return Json(new { success = false, message = "Database error updating status. Please try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error updating KYC ID {kycId}.");
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // --- NEW ACTION TO SERVE DOCUMENTS SECURELY (IF NOT IN WWWROOT) ---
        // If your kyc_documents are NOT in wwwroot, use this.
        // Adjust the path to your actual documents folder on the server.
        public IActionResult GetKycDocument(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest("File name cannot be empty.");
            }

            // !!! IMPORTANT: Adjust this path to your actual KYC documents storage !!!
            // Example: Assuming your kyc_documents folder is at the same level as your project's root
            // If the folder is outside the application's root, use a full absolute path:
            // var filePath = Path.Combine("D:\\YourServerPath\\kyc_documents", fileName);
            // If it's relative to the content root (where your .csproj is), you can use:
            var documentsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "kyc_documents");
            var filePath = Path.Combine(documentsFolderPath, fileName);


            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning($"Document not found: {filePath}");
                return NotFound();
            }

            // Determine content type based on file extension
            string contentType;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            switch (extension)
            {
                case ".pdf":
                    contentType = "application/pdf";
                    break;
                case ".png":
                    contentType = "image/png";
                    break;
                case ".jpg":
                case ".jpeg":
                    contentType = "image/jpeg";
                    break;
                case ".gif":
                    contentType = "image/gif";
                    break;
                // Add more types as needed
                default:
                    contentType = "application/octet-stream"; // Generic for unknown types, will prompt download
                    break;
            }

            return PhysicalFile(filePath, contentType);
        }

        public async Task<IActionResult> LoanApproval()
        {
            // Fetch all loan applications including associated customer and loan product details
            var loanApplications = await _context.LoanApplications
                                                .Include(la => la.Customer) // Include Customer details
                                                .Include(la => la.LoanProduct) // Include LoanProduct details
                                                .OrderByDescending(la => la.ApplicationDate) // Order by latest application first
                                                .ToListAsync();

            return View(loanApplications);
        }

        // POST: Admin/UpdateLoanStatus (for AJAX calls to update status)
        [HttpPost]
        public async Task<IActionResult> UpdateLoanStatus(int loanId, string status)
        {
            var loanApplication = await _context.LoanApplications.FindAsync(loanId);

            if (loanApplication == null)
            {
                return NotFound(new { success = false, message = "Loan application not found." });
            }

            // Validate status input to prevent invalid values
            if (status != "Approved" && status != "Rejected" && status != "Pending")
            {
                return BadRequest(new { success = false, message = "Invalid status provided." });
            }

            loanApplication.ApprovalStatus = status;

            // Set approval date only if status is approved or rejected
            if (status == "Approved" || status == "Rejected")
            {
                loanApplication.ApprovalDate = DateTime.Now;
            }
            else // If setting back to Pending, clear approval date
            {
                loanApplication.ApprovalDate = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, newStatus = loanApplication.ApprovalStatus, loanId = loanApplication.ApplicationId });
            }
            catch (DbUpdateConcurrencyException)
            {
                // Handle concurrency conflicts if multiple admins try to update at once
                return StatusCode(409, new { success = false, message = "Concurrency conflict: Loan was already updated. Please refresh." });
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using a logger)
                Console.WriteLine($"Error updating loan status: {ex.Message}");
                return StatusCode(500, new { success = false, message = "An error occurred while updating the loan status." });
            }
        }

        // GET: Admin/AddLoanProduct (Displays the form)
        [HttpGet]
        public IActionResult AddLoanProduct()
        {
            return View();
        }

        // POST: Admin/AddLoanProduct (Handles form submission)
        [HttpPost]
        [ValidateAntiForgeryToken] // Protects against CSRF attacks
        public async Task<IActionResult> AddLoanProduct(LoanProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if a product with the same name already exists
                if (await _context.LoanProducts.AnyAsync(p => p.ProductName == model.ProductName))
                {
                    ModelState.AddModelError("ProductName", "A loan product with this name already exists.");
                    return View(model);
                }

                var loanProduct = new LoanProduct
                {
                    ProductName = model.ProductName,
                    InterestRate = model.InterestRate,
                    MinAmount = model.MinAmount,
                    MaxAmount = model.MaxAmount,
                    Tenure = model.Tenure
                };

                _context.LoanProducts.Add(loanProduct);
                await _context.SaveChangesAsync();
                return RedirectToAction("LoanProducts", "Admin");
            }
            return View(model);
        }

        // GET: Admin/LoanProducts (Displays the Loan Products table page)
        [HttpGet]
        public IActionResult LoanProducts()
        {
            // The initial view load doesn't need to pass data
            // Data will be fetched via AJAX by the JavaScript in the view
            return View();
        }

        // API Endpoint: GET all loan products
        [HttpGet]
        public async Task<IActionResult> GetAllLoanProducts()
        {
            var products = await _context.LoanProducts
                                       .Select(p => new LoanProductViewModel
                                       {
                                           ProductName = p.ProductName,
                                           InterestRate = p.InterestRate,
                                           MinAmount = p.MinAmount,
                                           MaxAmount = p.MaxAmount,
                                           Tenure = p.Tenure
                                       })
                                       .ToListAsync();
            return Json(products);
        }

        // API Endpoint: GET a single loan product by name
        [HttpGet]
        public async Task<IActionResult> GetLoanProductByName(string productName)
        {
            if (string.IsNullOrEmpty(productName))
            {
                return BadRequest(new { success = false, message = "Product name cannot be empty." });
            }

            var product = await _context.LoanProducts
                                       .Where(p => p.ProductName == productName)
                                       .Select(p => new LoanProductViewModel
                                       {
                                           ProductName = p.ProductName,
                                           InterestRate = p.InterestRate,
                                           MinAmount = p.MinAmount,
                                           MaxAmount = p.MaxAmount,
                                           Tenure = p.Tenure
                                       })
                                       .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new { success = false, message = "Loan product not found." });
            }

            return Json(product);
        }

        // API Endpoint: POST to update a loan product
        [HttpPost]
        public async Task<IActionResult> UpdateLoanProduct([FromBody] LoanProductViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.ProductName))
            {
                return BadRequest(new { success = false, message = "Invalid product data." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                     .Select(e => e.ErrorMessage)
                                     .ToList();
                return BadRequest(new { success = false, message = string.Join("; ", errors) });
            }

            var existingProduct = await _context.LoanProducts.FirstOrDefaultAsync(p => p.ProductName == model.ProductName);

            if (existingProduct == null)
            {
                return NotFound(new { success = false, message = "Loan product not found." });
            }

            // Update properties
            existingProduct.InterestRate = model.InterestRate;
            existingProduct.MinAmount = model.MinAmount;
            existingProduct.MaxAmount = model.MaxAmount;
            existingProduct.Tenure = model.Tenure;

            try
            {
                _context.Update(existingProduct); // Mark the entity as modified
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Loan product updated successfully!" });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.LoanProducts.AnyAsync(e => e.ProductName == model.ProductName))
                {
                    return NotFound(new { success = false, message = "Loan product not found after update attempt." });
                }
                else
                {
                    // This error might indicate a concurrency issue (another user updated it).
                    // Log the error and return a generic message.
                    return StatusCode(500, new { success = false, message = "A concurrency error occurred. Please try again." });
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new { success = false, message = $"An error occurred while updating the loan product: {ex.Message}" });
            }
        }


        // API Endpoint: POST to delete a loan product
        [HttpPost]
        public async Task<IActionResult> DeleteLoanProduct(string productName)
        {
            if (string.IsNullOrEmpty(productName))
            {
                return BadRequest(new { success = false, message = "Product name cannot be empty." });
            }

            var loanProduct = await _context.LoanProducts.FirstOrDefaultAsync(p => p.ProductName == productName);

            if (loanProduct == null)
            {
                return NotFound(new { success = false, message = "Loan product not found." });
            }

            try
            {
                _context.LoanProducts.Remove(loanProduct);
                await _context.SaveChangesAsync();
                return Ok( new { success = true, message = $"deleted sucessfuly" });
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., if there are related records that prevent deletion)
                return StatusCode(500, new { success = false, message = $"An error occurred while deleting the loan product: {ex.Message}" });
            }
        }



    }
}