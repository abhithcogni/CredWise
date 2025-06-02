using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO; // <--- NEW: Using the ViewModel
using System.ComponentModel.DataAnnotations;


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
        [HttpGet]
        public async Task<IActionResult> LoanApplication()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            // Retrieve all loan products to populate the dropdown
            var loanProducts = await _context.LoanProducts.ToListAsync();
            ViewBag.LoanProducts = loanProducts; // Pass to the view

            return View();
        }

        // POST: Customer/ApplyForLoan
         // Good practice for security
        [HttpPost]
        [ValidateAntiForgeryToken] // Good practice for security
        public async Task<IActionResult> ApplyForLoan(int loanProductId, decimal loanAmount, int tenure)
        {
            // Ensure user is authenticated and is a customer
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return Unauthorized(); // Or RedirectToAction("Login", "Account");
            }

            // Get the CustomerId from the authenticated user's claims
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                ModelState.AddModelError("", "Unable to identify customer. Please log in again.");
                // Repopulate ViewBag.LoanProducts if returning to the same view
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }

            // Retrieve the selected loan product to get its details (interest rate, min/max amount, max tenure)
            var selectedLoanProduct = await _context.LoanProducts.FindAsync(loanProductId);
            if (selectedLoanProduct == null)
            {
                ModelState.AddModelError("", "Selected loan product is invalid.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                return View("LoanApplication");
            }

            // --- Server-Side Validation against LoanProduct limits ---
            // Ensure all required fields are provided and meet basic criteria
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
            if (tenure > selectedLoanProduct.Tenure) // Assuming LoanProduct.Tenure is max months
            {
                ModelState.AddModelError("tenure", $"Tenure cannot exceed the maximum allowed: {selectedLoanProduct.Tenure} months.");
            }

            // If any validation errors, return to the view
            if (!ModelState.IsValid)
            {
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync(); // Repopulate dropdown
                return View("LoanApplication");
            }

            // Create a new LoanApplication object and populate initial fields
            var loanApplication = new LoanApplication
            {
                CustomerId = customerId,
                LoanProductId = loanProductId,
                LoanAmount = loanAmount,
                ApplicationDate = DateTime.Now,
                ApprovalStatus = "Pending", // Initial status for the application

                // --- Populate NEWLY ADDED FIELDS from LoanProduct (at the time of application) ---
                // These capture the terms *at the time of application*
                InterestRate = selectedLoanProduct.InterestRate,
                TenureMonths = tenure, // This is the requested tenure
                LoanNumber = "APL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Generate a unique application reference number

                // --- Fields that are NOT populated at application submission ---
                // These will be populated AFTER loan approval/disbursal
                EMI = 0, // Will be calculated upon approval
                OutstandingBalance = 0, // Will be set to LoanAmount upon approval/disbursal
                NextDueDate = null, // Will be set upon approval/disbursal
                LastPaymentDate = null, // Will be set after the first payment
                AmountDue = 0, // Will be set to EMI upon approval/disbursal
                LoanStatus = "Pending Disbursal" // Initial status for the potential loan (different from ApprovalStatus)
            };

            try
            {
                _context.LoanApplications.Add(loanApplication);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your loan application has been submitted successfully!";
                return RedirectToAction("LoanStatus"); // Redirect to a success page or dashboard
            }
            catch (Exception ex)
            {
                // Log the exception (e.g., using a logging framework like Serilog, NLog)
                Console.WriteLine($"Error submitting loan application: {ex.Message}"); // For debugging
                ModelState.AddModelError("", "An unexpected error occurred while submitting your application. Please try again.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync(); // Repopulate dropdown
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
                // Handle case where customer ID is not found or not parsable
                TempData["ErrorMessage"] = "Unable to retrieve your loan applications. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Retrieve all loan applications for the current customer
            // Include LoanProduct to get the ProductName
            var customerLoanApplications = await _context.LoanApplications
                                                    .Where(la => la.CustomerId == customerId)
                                                    .Include(la => la.LoanProduct) // Eager load the related LoanProduct
                                                    .OrderByDescending(la => la.ApplicationDate)
                                                    .ToListAsync();

            // Pass the list of loan applications to the view
            return View(customerLoanApplications);
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
        public async Task<IActionResult> AcceptedLoans()
        {
            // Get logged-in CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                // If CustomerId claim is not found or invalid, redirect to login or error
                TempData["ErrorMessage"] = "Authentication error: Customer ID not found.";
                return RedirectToAction("Login", "Account"); // Or appropriate error page
            }

            var approvedLoans = await _context.LoanApplications
                                        .Include(l => l.LoanProduct) // Include LoanProduct if it's a navigation property
                                        .Where(l => l.CustomerId == customerId && l.ApprovalStatus == "Approved")
                                        .ToListAsync();

            return View(approvedLoans);
        }

         // This action handles the form submission for updating customer details
        // Protect against Cross-Site Request Forgery attacks
        // <--- NEW: Model bind to the ViewModel instead of the domain model --->
        public async Task<IActionResult> MakePayment(int loanId)
        {
            // Get logged-in CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                // If CustomerId claim is not found or invalid, redirect to login or error
                TempData["ErrorMessage"] = "Authentication error: Customer ID not found.";
                return RedirectToAction("Login", "Account"); // Or appropriate error page
            }

            var loan = await _context.LoanApplications
                                     .FirstOrDefaultAsync(l => l.ApplicationId == loanId && l.CustomerId == currentCustomerId);

            if (loan == null)
            {
                // Handle loan not found or not belonging to the customer
                TempData["ErrorMessage"] = "Loan not found or you do not have permission to access it.";
                return RedirectToAction("AcceptedLoans"); // Redirect back to the accepted loans list
            }

            // Prepare a ViewModel to pass data to the view
            var model = new MakePaymentViewModel
            {
                LoanId = loan.ApplicationId,
                ShortDescription = "Your monthly installment for your loan.",
                AmountDue = loan.AmountDue, // Use the AmountDue from the loan record
                DueDate = loan.NextDueDate,
                OutstandingBalance = loan.OutstandingBalance,
                MinPayment = loan.EMI // The EMI is typically the minimum payment
            };

            // Determine if a payment is currently due
            // A payment is due if AmountDue > 0 and NextDueDate is on or before today
            if (loan.AmountDue > 0 && loan.NextDueDate.HasValue && loan.NextDueDate.Value.Date <= DateTime.Now.Date)
            {
                model.IsPaymentDue = true;
            }
            else
            {
                model.IsPaymentDue = false;
            }

            return View("makepayment", model); // Ensure your view name matches (makepayment.cshtml)
        }

        // POST: /Loan/ProcessPayment (This will be called via AJAX)
        [HttpPost]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get logged-in CustomerId from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                // If CustomerId claim is not found or invalid, return unauthorized
                return Unauthorized(new { success = false, message = "Authentication error: Customer ID not found." });
            }

            var loan = await _context.LoanApplications
                                     .FirstOrDefaultAsync(l => l.ApplicationId == request.LoanId && l.CustomerId == currentCustomerId);

            if (loan == null)
            {
                return NotFound(new { success = false, message = "Loan not found or does not belong to the customer." });
            }

            // Server-side validation
            if (request.PaymentAmount <= 0)
            {
                return BadRequest(new { success = false, message = "Payment amount must be positive." });
            }

            // If a payment is currently due (loan.AmountDue > 0)
            if (loan.AmountDue > 0)
            {
                if (request.PaymentAmount < loan.EMI) // Enforce minimum EMI, or implement partial payment logic carefully
                {
                    // If partial payments are allowed, this logic needs refinement.
                    // For now, assuming EMI is the minimum expected payment.
                    return BadRequest(new { success = false, message = $"Minimum payment amount for this installment is ${loan.EMI:F2}." });
                }
            }
            // General check: payment amount cannot exceed outstanding balance
            if (request.PaymentAmount > loan.OutstandingBalance)
            {
                return BadRequest(new { success = false, message = $"Payment amount cannot exceed the outstanding balance of ${loan.OutstandingBalance:F2}." });
            }

            // --- Simulate Payment Gateway Integration ---
            bool paymentGatewaySuccess = SimulatePaymentGateway(request.PaymentAmount, request.PaymentMethod);

            if (paymentGatewaySuccess)
            {
                // Create a new payment record
                var newPayment = new LoanPayment
                {
                    LoanId = loan.ApplicationId,
                    CustomerId = currentCustomerId, // Use the actual logged-in customer ID
                    PaidAmount = request.PaymentAmount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = "TRX" + Guid.NewGuid().ToString().Substring(0, 8), // Mock Transaction ID
                    Status = "Success"
                };
                _context.LoanPayments.Add(newPayment);

                // Update loan details in a transaction
                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        loan.OutstandingBalance -= request.PaymentAmount;
                        loan.LastPaymentDate = DateTime.Now;

                        // Logic to handle EMI payment and next due date
                        if (request.PaymentAmount >= loan.AmountDue && loan.AmountDue > 0)
                        {
                            // Assume the current due amount is covered
                            loan.AmountDue = 0;
                            // Advance next due date by one month
                            if (loan.NextDueDate.HasValue)
                            {
                                loan.NextDueDate = loan.NextDueDate.Value.AddMonths(1);
                            }
                            else
                            {
                                // If NextDueDate was null, set it to one month from now
                                loan.NextDueDate = DateTime.Now.AddMonths(1);
                            }
                        }
                        else if (request.PaymentAmount < loan.AmountDue && loan.AmountDue > 0)
                        {
                            // Partial payment: reduce amount due but don't advance NextDueDate
                            loan.AmountDue -= request.PaymentAmount;
                        }
                        // If no amount was due, but customer makes an advance payment
                        else if (loan.AmountDue == 0 && request.PaymentAmount > 0)
                        {
                            // This case would typically be handled as an "advance payment"
                            // You might not update AmountDue immediately, but reduce OutstandingBalance.
                            // Or, if this is a payment for a future EMI, you'd mark it accordingly.
                            // For simplicity, we've already reduced outstanding balance.
                            // No change to AmountDue or NextDueDate needed here if it's an "advance".
                        }


                        // Check if loan is fully paid
                        if (loan.OutstandingBalance <= 0)
                        {
                            loan.OutstandingBalance = 0; // Ensure it's not negative
                            loan.LoanStatus = "Closed";
                            loan.EMI = 0;
                            loan.AmountDue = 0;
                            loan.NextDueDate = null; // No more due dates
                        }

                        await _context.SaveChangesAsync();
                        transaction.Commit(); // Commit transaction if all successful
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback(); // Rollback if any error occurs
                        // Log the exception
                        Console.WriteLine($"Error processing payment: {ex.Message}");
                        return StatusCode(500, new { success = false, message = "An error occurred while updating loan details.", reason = ex.Message });
                    }
                }

                return Ok(new { success = true, message = "Payment successful!", transactionId = newPayment.TransactionId });
            }
            else
            {
                // Payment Gateway Failed
                var failedPayment = new LoanPayment
                {
                    LoanId = loan.ApplicationId,
                    CustomerId = currentCustomerId, // Use the actual logged-in customer ID
                    PaidAmount = request.PaymentAmount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = "FAILTRX" + Guid.NewGuid().ToString().Substring(0, 8),
                    Status = "Failed"
                };
                _context.LoanPayments.Add(failedPayment);
                await _context.SaveChangesAsync(); // Save failed payment attempt

                return StatusCode(500, new { success = false, message = "Payment failed at gateway. Please try again.", reason = "Simulated insufficient funds." });
            }
        }


        // Helper method to simulate a payment gateway response
        private bool SimulatePaymentGateway(decimal amount, string method)
        {
            // Simulate a delay
            System.Threading.Thread.Sleep(2000); // Simulate 2 seconds delay

            // Simulate success/failure (85% success rate)
            return new Random().NextDouble() < 0.85;
        }
    }

    // ViewModel to pass data from Controller to View
    public class MakePaymentViewModel
    {
        public int LoanId { get; set; }
        public string ProductName { get; set; }
        public string ShortDescription { get; set; }
        public decimal AmountDue { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal MinPayment { get; set; }
        public bool IsPaymentDue { get; set; }
    }

    // Data Transfer Object for incoming payment request from frontend (AJAX)
    public class PaymentRequestDto
    {
        [Required]
        public int LoanId { get; set; }
        [Required]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Payment amount must be greater than zero.")]
        public decimal PaymentAmount { get; set; }
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }
        // Add other properties for new payment methods if needed (e.g., CardNumber, ExpiryDate, CVV)
    }

}

