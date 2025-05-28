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

        public IActionResult LoanApproval()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AddLoanProduct()
        {
            return View();
        }

       

        [HttpGet]
        public async Task<IActionResult> LoanProducts()
        {
           
            return View();
        }

        // ====== MODIFIED EDIT ACTIONS ======

        // GET: Admin/EditLoanProduct/5 - Displays the edit form
        
       

        
    }
}