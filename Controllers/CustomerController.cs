using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization; // Added for LINQ operations

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
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LoanApplication()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirstValue("CustomerId"); // Or ClaimTypes.NameIdentifier if that's what you use
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account"); // Or a more appropriate error view/redirect
            }

            // Fetch the most recent KYC record for the customer
            var latestKyc = await _context.KycApprovals
                                        .Where(k => k.CustomerId == customerId)
                                        .OrderByDescending(k => k.SubmissionDate)
                                        .FirstOrDefaultAsync();

            ViewData["ShowLoanForm"] = false; // Default: do not show the loan form

            if (latestKyc != null)
            {
                ViewData["KycStatus"] = latestKyc.Status;
                switch (latestKyc.Status)
                {
                    case "Approved":
                        ViewData["ShowLoanForm"] = true;
                        // Only load loan products if KYC is approved
                        var loanProducts = await _context.LoanProducts.ToListAsync();
                        ViewBag.LoanProducts = loanProducts; // This is used by your view
                        break;
                    case "Pending":
                        TempData["WarningMessage"] = "Your KYC verification is currently pending. It must be approved before you can apply for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer"); // Link to your KYC page
                        ViewData["KycPageLinkText"] = "Check KYC Status / Upload Documents";
                        break;
                    case "Rejected":
                        TempData["ErrorMessage"] = "Your KYC verification was rejected. Please re-submit your KYC documents and get approval before applying for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer"); // Link to your KYC page
                        ViewData["KycPageLinkText"] = "Re-apply for KYC";
                        break;
                    default: // Handles any other status
                        TempData["InfoMessage"] = $"Your KYC status is '{latestKyc.Status}'. Please ensure it is approved to apply for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer");
                        ViewData["KycPageLinkText"] = "Review KYC Status";
                        break;
                }
            }
            else // No KYC record found for the customer
            {
                ViewData["KycStatus"] = "Not Submitted";
                TempData["InfoMessage"] = "You need to complete and get your KYC verified before applying for a loan.";
                ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer"); // Link to your KYC page
                ViewData["KycPageLinkText"] = "Complete KYC Verification";
            }

            // The view will now use ViewData["ShowLoanForm"] to decide what to display
            return View(); // If ShowLoanForm is true, it will expect ViewBag.LoanProducts
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyForLoan(int loanProductId, decimal loanAmount, int tenure)
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return Unauthorized();
            }

            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                ModelState.AddModelError("", "Unable to identify customer. Please log in again.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }

            var selectedLoanProduct = await _context.LoanProducts.FindAsync(loanProductId);
            if (selectedLoanProduct == null)
            {
                ModelState.AddModelError("", "Selected loan product is invalid.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }

            if (loanAmount <= 0)
            {
                ModelState.AddModelError("loanAmount", "Loan amount must be a positive value.");
            }
            if (loanAmount < selectedLoanProduct.MinAmount)
            {
                ModelState.AddModelError("loanAmount", $"Loan amount cannot be less than the minimum required: {selectedLoanProduct.MinAmount:C}.");
            }
            if (loanAmount > selectedLoanProduct.MaxAmount)
            {
                ModelState.AddModelError("loanAmount", $"Loan amount cannot exceed the maximum allowed: {selectedLoanProduct.MaxAmount:C}.");
            }

            if (tenure <= 0)
            {
                ModelState.AddModelError("tenure", "Tenure must be a positive value.");
            }
            if (tenure > selectedLoanProduct.Tenure)
            {
                ModelState.AddModelError("tenure", $"Tenure cannot exceed the maximum allowed: {selectedLoanProduct.Tenure} months.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }

            var loanApplication = new LoanApplication
            {
                CustomerId = customerId,
                LoanProductId = loanProductId,
                LoanAmount = loanAmount,
                ApplicationDate = DateTime.Now,
                ApprovalStatus = "Pending",
                InterestRate = selectedLoanProduct.InterestRate,
                TenureMonths = tenure,
                LoanNumber = "APL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),

                // These will be calculated and set upon actual loan approval/disbursement
                EMI = 0,
                OutstandingBalance = 0,
                NextDueDate = null,
                LastPaymentDate = null,
                AmountDue = 0,
                LoanStatus = "Pending Disbursement", // Initial status for the potential loan
                OverdueMonths = 0,
                CurrentOverdueAmount = 0
            };

            try
            {
                _context.LoanApplications.Add(loanApplication);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your loan application has been submitted successfully!";
                return RedirectToAction("LoanStatus");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting loan application: {ex.Message}");
                ModelState.AddModelError("", "An unexpected error occurred while submitting your application. Please try again.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }
        }

        public IActionResult CustomerStatements()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        public async Task<IActionResult> LoanStatus()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Unable to retrieve your loan applications. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            var customerLoanApplications = await _context.LoanApplications
                                                            .Where(la => la.CustomerId == customerId)
                                                            .Include(la => la.LoanProduct)
                                                            .OrderByDescending(la => la.ApplicationDate)
                                                            .ToListAsync();

            return View(customerLoanApplications);
        }

        [HttpGet]
        [Authorize(Roles = "Customer")] // Ensures only authenticated customers can access
        public async Task<IActionResult> KYCUpload()
        {
            // User.Identity.IsAuthenticated check is implicitly handled by [Authorize]
            // User.IsInRole("Customer") is also handled by [Authorize(Roles="Customer")]

            var customerIdClaim = User.FindFirst("CustomerId"); // Or User.FindFirst(ClaimTypes.NameIdentifier) if that's what you use
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                // It might be better to redirect to Login if customerId is crucial and missing,
                // or ensure it's always present for authenticated customers.
                return RedirectToAction("Logout", "Account"); // Or an error page
            }

            // Fetch the most recent KYC record for the customer
            var latestKyc = await _context.KycApprovals
                                        .Where(k => k.CustomerId == customerId)
                                        .OrderByDescending(k => k.SubmissionDate)
                                        .FirstOrDefaultAsync();

            var model = new KycUploadViewModel(); // Initialize model for the view

            if (latestKyc != null)
            {
                ViewData["CurrentKycStatus"] = latestKyc.Status; // Store current status for display
                switch (latestKyc.Status)
                {
                    case "Approved":
                        ViewData["ShowForm"] = false; // Flag to hide form
                        TempData["InfoMessage"] = "Your KYC has already been approved. No further action is required.";
                        // Optionally, redirect to dashboard if no other info needs to be shown on this page
                        // return RedirectToAction("CustomerDashboard");
                        break;
                    case "Pending":
                        ViewData["ShowForm"] = true; // Flag to show form (allows resubmission)
                        TempData["InfoMessage"] = "Your KYC submission is currently pending review. You can upload new documents if you wish to replace the previous submission.";
                        break;
                    case "Rejected":
                        ViewData["ShowForm"] = true; // Flag to show form for resubmission
                        TempData["WarningMessage"] = "Your previous KYC submission was rejected. Please review any feedback and upload the correct documents again.";
                        break;
                    default: // Handles any other unforeseen status or if a new submission is desired
                        ViewData["ShowForm"] = true;
                        break;
                }
            }
            else // No existing KYC record, it's a new submission
            {
                ViewData["ShowForm"] = true;
                ViewData["CurrentKycStatus"] = "Not Submitted"; // For display purposes
                                                                // No specific TempData message needed here, the standard form instructions will show.
            }

            return View(model);
        }

        // Your existing HttpPost KYCUpload action remains largely the same.
        // It will create a new KYC record with "Pending" status upon each submission.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> KYCUpload(KycUploadViewModel model)
        {
            // Existing authentication and customerId retrieval logic
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            // Check if an "Approved" KYC already exists. If so, prevent new submissions.
            // This is an additional safeguard in the POST, complementing the GET logic.
            var existingApprovedKyc = await _context.KycApprovals
                                            .AnyAsync(k => k.CustomerId == customerId && k.Status == "Approved");
            if (existingApprovedKyc)
            {
                TempData["InfoMessage"] = "Your KYC is already approved. You cannot submit new documents.";
                return RedirectToAction("CustomerDashboard"); // Or back to the KYCUpload page which will show the approved message
            }


            if (ModelState.IsValid)
            {
                string contentRootPath = Directory.GetCurrentDirectory(); // Consider using IWebHostEnvironment
                string uploadFolder = Path.Combine(contentRootPath, "kyc_documents"); // Store in wwwroot for easier serving if needed

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                try
                {
                    string identityFileName = null;
                    if (model.IdentityProofFile != null && model.IdentityProofFile.Length > 0)
                    {
                        // Basic validation (you have client-side, but server-side is crucial)
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                        var fileExtension = Path.GetExtension(model.IdentityProofFile.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(fileExtension))
                        {
                            ModelState.AddModelError("IdentityProofFile", "Invalid file type. Only JPG, PNG, PDF are allowed.");
                            TempData["ErrorMessage"] = "Invalid file type submitted.";
                            return View(model);
                        }

                        long maxFileSize = 5 * 1024 * 1024; // 5MB
                        if (model.IdentityProofFile.Length > maxFileSize)
                        {
                            ModelState.AddModelError("IdentityProofFile", "File size exceeds the 5MB limit.");
                            TempData["ErrorMessage"] = "File size exceeds the 5MB limit.";
                            return View(model);
                        }

                        identityFileName = $"{customerId}_{Guid.NewGuid()}_identity{fileExtension}";
                        string filePath = Path.Combine(uploadFolder, identityFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.IdentityProofFile.CopyToAsync(fileStream);
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("IdentityProofFile", "Identity proof document is required.");
                        TempData["ErrorMessage"] = "Identity proof document is required.";
                        return View(model); // Stay on the view if the file is missing
                    }

                    var kycApproval = new KycApproval
                    {
                        CustomerId = customerId,
                        SubmissionDate = DateTime.UtcNow,
                        Status = "Pending", // New submissions are always Pending
                        DocumentPath = identityFileName, // Relative path if stored in wwwroot, or full path
                                                         // DocumentType = model.IdentityDocumentType // Assuming you add this to KycApproval model if needed
                    };

                    _context.KycApprovals.Add(kycApproval);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "KYC documents uploaded successfully! Your verification is pending review.";
                    return RedirectToAction("CustomerDashboard");
                }
                catch (Exception ex)
                {
                    // Log the exception (ex)
                    Console.WriteLine($"Error uploading KYC documents: {ex.Message}"); // Basic logging
                    TempData["ErrorMessage"] = "An error occurred during document upload. Please try again.";
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }
            else
            {
                // Log ModelState errors for debugging
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"ModelState Error: {state.Key} - {error.ErrorMessage}");
                    }
                }
                TempData["ErrorMessage"] = "Please correct the errors below and try again.";
            }

            // If ModelState is invalid or an exception occurred, return to the view with the model
            // The view will then display the validation errors and TempData messages
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerDetails()
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

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer record not found. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }
            if (TempData["ErrorMessage"] != null)
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
            }

            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerUpdate()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer for update. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            var customerToUpdate = await _context.Customers.FindAsync(customerId);
            if (customerToUpdate == null)
            {
                TempData["ErrorMessage"] = "Customer record not found for update. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            var viewModel = new CustomerUpdateViewModel
            {
                CustomerId = customerToUpdate.CustomerId,
                Name = customerToUpdate.Name,
                Email = customerToUpdate.Email,
                PhoneNumber = customerToUpdate.PhoneNumber,
                Address = customerToUpdate.Address
            };

            return View(viewModel);
        }

        // This action displays the list of accepted loans and simulates their disbursement
        [HttpGet]
        public async Task<IActionResult> MakePayment(int loanApplicationId)
        {
            // IMPORTANT: Ensure this loan belongs to the logged-in customer.
            // Example:
            // var customerIdClaim = User.FindFirst("CustomerId"); // Or ClaimTypes.NameIdentifier
            // if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId)) {
            //     TempData["ErrorMessage"] = "Authentication error.";
            //     return RedirectToAction("Login", "Account"); // Or an appropriate error/access denied page
            // }

            var customerIdClaim = User.FindFirst("CustomerId"); // Ensure "CustomerId" claim is correctly set during login
                                                                // Alternatively, use ClaimTypes.NameIdentifier if that stores your numeric Customer ID
            Console.WriteLine("loanID:",loanApplicationId);
            Console.WriteLine("customerId:",customerIdClaim);
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Authentication error: Unable to identify customer.";
                // Redirect to login or an appropriate error page.
                // Assuming you have a Login action in an Account controller:
                return RedirectToAction("Login", "Account");
            }
            // 2. Fetch the specific loan application for the authenticated customer
            var loanApplication = await _context.LoanApplications
                                        .Include(la => la.LoanProduct) // Needed to display product name
                                                                       // .Include(la => la.Customer) // Include if other customer details are needed on this page
                                        .FirstOrDefaultAsync(la => la.ApplicationId == loanApplicationId && la.CustomerId == customerId);

            

            if (loanApplication == null)
            {
                TempData["ErrorMessage"] = "Loan application not found.";
                return RedirectToAction("AcceptedLoans"); // Redirect to list of customer's active loans
            }

            if (loanApplication.LoanStatus == "Closed" || loanApplication.OutstandingBalance <= 0)
            {
                ViewBag.NoPaymentDueMessage = "This loan is fully paid and closed.";
            }
            else if (loanApplication.LoanStatus == "Pending Disbursement" || loanApplication.LoanStatus == "Pending")
            {
                ViewBag.NoPaymentDueMessage = "This loan is not yet active for payments.";
            }

            return View("~/Views/CustomerPortal/MakePayment.cshtml", loanApplication);
        }

        // POST: /Customer/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int loanId, decimal paidAmount, string paymentMethod)
        {
            // IMPORTANT: Validate that loanId belongs to the currently authenticated customer.
            // Example:
            // var customerIdClaim = User.FindFirst("CustomerId");
            // if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId)) {
            //     return Json(new { success = false, message = "User not authenticated." });
            // }

            if (paidAmount <= 0)
            {
                return Json(new { success = false, message = "Payment amount must be positive." });
            }

            var loanApplication = await _context.LoanApplications
                                          .Include(la => la.Repayments.Where(r => r.PaymentStatus == "PENDING").OrderBy(r => r.DueDate)) // Eager load pending repayments
                                          .FirstOrDefaultAsync(la => la.ApplicationId == loanId /* && la.CustomerId == currentCustomerId */); // Uncomment customerId check

            if (loanApplication == null)
            {
                return Json(new { success = false, message = "Loan application not found." });
            }

            if (loanApplication.LoanStatus == "Closed")
            {
                return Json(new { success = false, message = "This loan is already closed." });
            }
            if (loanApplication.LoanStatus == "Pending Disbursement" || loanApplication.LoanStatus == "Pending")
            {
                return Json(new { success = false, message = "This loan is not yet active for payments." });
            }

            // --- 1. Record the Payment Transaction ---
            var payment = new LoanPayment
            {
                LoanId = loanApplication.ApplicationId, // FK to LoanApplication's PK
                CustomerId = loanApplication.CustomerId,
                PaidAmount = paidAmount,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                TransactionId = $"MOCKTRX{DateTime.Now.Ticks}", // Placeholder: Generate a real transaction ID
                Status = "Success" // Placeholder: Set based on actual payment gateway response
            };
            _context.LoanPayments.Add(payment);
            // Ensure LoanPayment.LoanId ForeignKey attribute correctly maps to LoanApplication.ApplicationId.

            // --- 2. Apply Payment to Repayments and Update Loan Application ---
            decimal remainingAmountToAllocate = paidAmount;
            bool duesClearedThisTransaction = false;

            // Handle overdue payments first
            if (loanApplication.LoanStatus == "Overdue")
            {
                var pendingOverdueRepayments = loanApplication.Repayments.ToList(); // Already filtered for PENDING, ordered by DueDate

                foreach (var repayment in pendingOverdueRepayments)
                {
                    if (remainingAmountToAllocate <= 0) break;

                    decimal amountToApplyToThisRepayment = Math.Min(remainingAmountToAllocate, repayment.AmountDue);
                    // Interest calculation for each overdue EMI should follow specific business rules.
                    // This example calculates simple interest on the outstanding balance before this EMI's principal reduction.
                    decimal interestForThisEmiPeriod = CalculateInterestForPeriod(loanApplication.OutstandingBalance, loanApplication.InterestRate);
                    decimal principalFromThisEmi = Math.Max(0, amountToApplyToThisRepayment - interestForThisEmiPeriod);
                    principalFromThisEmi = Math.Min(principalFromThisEmi, loanApplication.OutstandingBalance); // Cannot pay more principal than available

                    loanApplication.OutstandingBalance -= principalFromThisEmi;
                    repayment.PaymentDate = DateTime.Now;
                    repayment.PaymentStatus = "COMPLETED";
                    _context.Repayments.Update(repayment);
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
                if (loanApplication.OutstandingBalance < 0) loanApplication.OutstandingBalance = 0;

                // Check if all past dues are now cleared
                var stillOverdueRepayments = await _context.Repayments
                                                .Where(r => r.ApplicationId == loanApplication.ApplicationId &&
                                                            r.PaymentStatus == "PENDING" &&
                                                            r.DueDate.Date < DateTime.Now.Date)
                                                .ToListAsync();
                if (!stillOverdueRepayments.Any())
                {
                    loanApplication.LoanStatus = "Active";
                    loanApplication.OverdueMonths = 0;
                    loanApplication.CurrentOverdueAmount = 0;
                    duesClearedThisTransaction = true;
                }
                else
                { // Still overdue, update overdue details
                    loanApplication.CurrentOverdueAmount = stillOverdueRepayments.Sum(r => r.AmountDue);
                    loanApplication.OverdueMonths = stillOverdueRepayments.Count(); // Number of overdue EMIs
                }
            }

            // Apply to current/future EMIs if loan is Active or dues were just cleared, and funds remain
            if ((loanApplication.LoanStatus == "Active" || duesClearedThisTransaction) && remainingAmountToAllocate > 0)
            {
                var nextPendingRepayments = await _context.Repayments
                                .Where(r => r.ApplicationId == loanApplication.ApplicationId && r.PaymentStatus == "PENDING")
                                .OrderBy(r => r.DueDate)
                                .ToListAsync();

                foreach (var repayment in nextPendingRepayments)
                {
                    if (remainingAmountToAllocate <= 0) break;
                    // Assumes payment covers full EMIs. Partial payment of a single EMI needs more complex logic.
                    decimal amountToApplyToThisRepayment = Math.Min(remainingAmountToAllocate, repayment.AmountDue);
                    decimal interestForThisEmiPeriod = CalculateInterestForPeriod(loanApplication.OutstandingBalance, loanApplication.InterestRate);
                    decimal principalFromThisEmi = Math.Max(0, amountToApplyToThisRepayment - interestForThisEmiPeriod);
                    principalFromThisEmi = Math.Min(principalFromThisEmi, loanApplication.OutstandingBalance);

                    loanApplication.OutstandingBalance -= principalFromThisEmi;
                    repayment.PaymentDate = DateTime.Now;
                    repayment.PaymentStatus = "COMPLETED";
                    _context.Repayments.Update(repayment);
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
                if (loanApplication.OutstandingBalance < 0) loanApplication.OutstandingBalance = 0;
            }

            loanApplication.LastPaymentDate = DateTime.Now;

            // --- 3. Update Loan Application's Next Due Date and Amount Due ---
            var nextScheduledRepayment = await _context.Repayments
                                    .Where(r => r.ApplicationId == loanApplication.ApplicationId && r.PaymentStatus == "PENDING")
                                    .OrderBy(r => r.DueDate)
                                    .FirstOrDefaultAsync();

            if (nextScheduledRepayment != null)
            {
                loanApplication.NextDueDate = nextScheduledRepayment.DueDate;
                loanApplication.AmountDue = nextScheduledRepayment.AmountDue;
            }
            else // No more pending repayments
            {
                loanApplication.NextDueDate = null;
                loanApplication.AmountDue = 0;
                if (loanApplication.OutstandingBalance <= 0.01m) // Using a small threshold for decimal precision
                {
                    loanApplication.OutstandingBalance = 0; // Ensure exact zero
                    loanApplication.LoanStatus = "Closed"; // Loan Closure
                    loanApplication.CurrentOverdueAmount = 0;
                    loanApplication.OverdueMonths = 0;
                }
            }

            // --- 4. Update Overdue Status (Conceptual: Typically handled by a separate batch job) ---
            // This section simulates a check that a nightly batch job might perform.
            if (loanApplication.LoanStatus != "Closed" && loanApplication.LoanStatus != "Overdue")
            { // If not already closed or marked overdue by payment logic
                var firstPendingPastDueInstallment = await _context.Repayments
                    .Where(r => r.ApplicationId == loanApplication.ApplicationId &&
                                 r.PaymentStatus == "PENDING" &&
                                 r.DueDate.Date < DateTime.Now.Date)
                    .OrderBy(r => r.DueDate)
                    .FirstOrDefaultAsync();

                if (firstPendingPastDueInstallment != null)
                {
                    loanApplication.LoanStatus = "Overdue";
                    var allPendingPastDues = await _context.Repayments
                        .Where(r => r.ApplicationId == loanApplication.ApplicationId &&
                                     r.PaymentStatus == "PENDING" &&
                                     r.DueDate.Date < DateTime.Now.Date)
                        .ToListAsync();
                    loanApplication.OverdueMonths = allPendingPastDues.Count();
                    loanApplication.CurrentOverdueAmount = allPendingPastDues.Sum(r => r.AmountDue);
                    // Adjust overall AmountDue on LoanApplication based on overdue status
                    loanApplication.AmountDue = loanApplication.CurrentOverdueAmount;
                    if (nextScheduledRepayment != null && nextScheduledRepayment.DueDate.Date >= DateTime.Now.Date)
                    {
                        loanApplication.AmountDue += nextScheduledRepayment.AmountDue; // Add current month's EMI if applicable
                    }
                }
            }
            // --- End of Conceptual Overdue Check ---

            try
            {
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = $"Payment of INR {paidAmount:N2} processed successfully.",
                    loanStatus = loanApplication.LoanStatus,
                    outstandingBalance = loanApplication.OutstandingBalance,
                    nextDueDate = loanApplication.NextDueDate?.ToString("yyyy-MM-dd"),
                    amountDue = loanApplication.AmountDue
                });
            }
            catch (Exception ex)
            {
                // Log ex for detailed error diagnosis
                Console.WriteLine($"Error processing payment for LoanId {loanId}: {ex.Message} {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while processing the payment." });
            }
        }

        // Helper method to calculate interest for one month based on current balance and annual rate.
        private decimal CalculateInterestForPeriod(decimal currentOutstandingBalance, decimal annualInterestRatePercentage)
        {
            if (currentOutstandingBalance <= 0) return 0;
            decimal monthlyInterestRate = annualInterestRatePercentage / 12 / 100;
            return Math.Round(currentOutstandingBalance * monthlyInterestRate, 2);
        }

        // GET: /Customer/AcceptedLoans
        // Displays loans that are approved and not yet closed for the authenticated customer.
        public async Task<IActionResult> AcceptedLoans()
        {
            var customerIdClaim = User.FindFirst("CustomerId"); // Assumes "CustomerId" claim is set during login
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Authentication error: Customer ID not found.";
                return RedirectToAction("Login", "Account"); // Redirect to login if not authenticated
            }

            var approvedLoans = await _context.LoanApplications
                                            .Include(l => l.LoanProduct) // To display product details
                                            .Where(l => l.CustomerId == customerId && l.ApprovalStatus == "Approved")
                                            .Where(l => l.LoanStatus != "Closed") // Filter out already closed loans
                                            .ToListAsync();

            // This section simulates a disbursement check if your workflow includes 'Pending Disbursement' status
            // and needs to transition to 'Active' upon viewing or a similar trigger.
            // In a real system, disbursement might be an explicit admin action or automated process.
            bool hasChanges = false;
            foreach (var loan in approvedLoans)
            {
                if (loan.LoanStatus == "Pending Disbursement") // A status indicating approved but funds not yet released
                {
                    // await SimulateLoanDisbursement(loan); // Your custom logic for disbursement
                    // Example: Update status and outstanding balance if disbursement logic is here
                    // loan.LoanStatus = "Active";
                    // loan.OutstandingBalance = loan.LoanAmount; // If not set already
                    // loan.NextDueDate = ... // Set first due date if not set during approval
                    // hasChanges = true;
                    // This is a placeholder for your disbursement logic which might also generate the schedule
                    // if not done directly in UpdateLoanStatus. For this flow, schedule is in UpdateLoanStatus.
                }
            }
            if (hasChanges)
            {
                await _context.SaveChangesAsync(); // Save changes if any loan statuses were updated
            }

            return View(approvedLoans); // Pass the list of loans to an AcceptedLoans.cshtml view
        }
    }
}