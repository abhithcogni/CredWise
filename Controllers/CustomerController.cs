using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using CredWise_Trail.Models.ViewModels; // Added for LINQ operations

namespace CredWise_Trail.Controllers
{
    public class CustomerController : Controller
    {
        private readonly BankLoanManagementDbContext _context;

        public CustomerController(BankLoanManagementDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> CustomerDashboard()
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

            var viewModel = new CustomerDashboardViewModel();

            // 1. Fetch a LIST of ALL loans for the customer that are "Active" OR "Overdue".
            var relevantLoans = await _context.LoanApplications
                .Where(l => l.CustomerId == customerId &&
                            (l.LoanStatus == "Active" || l.LoanStatus == "Overdue")) // MODIFIED: Include "Overdue" loans
                .ToListAsync();

            if (relevantLoans != null && relevantLoans.Any())
            {
                viewModel.HasActiveLoans = true; // Renaming this might be good, but keeping for now.
                                                 // It now means "Has Active or Overdue Loans"
                viewModel.ActiveLoanCount = relevantLoans.Count; // This will count both active and overdue

                // Calculations for the summary card
                viewModel.TotalPrincipalAmount = relevantLoans.Sum(l => l.LoanAmount);
                viewModel.TotalOutstandingBalance = relevantLoans.Sum(l => l.OutstandingBalance);
                viewModel.TotalNextPaymentAmount = relevantLoans.Sum(l => l.AmountDue);
                viewModel.EarliestNextDueDate = relevantLoans.Min(l => l.NextDueDate);

                if (viewModel.TotalPrincipalAmount > 0)
                {
                    var totalAmountPaid = viewModel.TotalPrincipalAmount - viewModel.TotalOutstandingBalance;
                    viewModel.OverallProgressPercentage = (int)Math.Round((totalAmountPaid / viewModel.TotalPrincipalAmount) * 100);
                }
                else
                {
                    viewModel.OverallProgressPercentage = 0; // Handle division by zero if all loans are fully paid
                }


                var relevantLoanIds = relevantLoans.Select(l => l.ApplicationId).ToList();

                // THIS IS THE CORRECTED QUERY for recent payments to include relevant loans
                var recentPayments = await _context.LoanPayments
                    .Include(p => p.LoanApplication)      // 1. Include the related LoanApplication
                        .ThenInclude(la => la.LoanProduct) // 2. THEN, include the LoanProduct from that LoanApplication
                    .Where(p => relevantLoanIds.Contains(p.LoanId)) // Use relevantLoanIds
                    .OrderByDescending(p => p.PaymentDate)
                    .Take(5)
                    .ToListAsync();

                // Populate the recent payments list for the view.
                foreach (var payment in recentPayments)
                {
                    viewModel.RecentPayments.Add(new RecentPaymentItem
                    {
                        PaymentDate = payment.PaymentDate,
                        Description = $"Payment for {payment.LoanApplication.LoanProduct.ProductName} ({payment.LoanApplication.LoanNumber})",
                        PaidAmount = payment.PaidAmount,
                        Status = payment.Status
                    });
                }
            }
            else
            {
                viewModel.HasActiveLoans = false;
            }

            return View("CustomerDashboard", viewModel);
        }
    

        // Add this new action to your CustomerController class

        public async Task<IActionResult> AllPayments()
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
            var viewModel = new AllPaymentsViewModel();

            // 1. Get all loan IDs associated with the current customer.
            // We get all loans, not just active, as they might want history for closed loans.
            var customerLoanIds = await _context.LoanApplications
                .Where(l => l.CustomerId == customerId)
                .Select(l => l.ApplicationId)
                .ToListAsync();

            // 2. Check if the customer has any loans at all.
            if (customerLoanIds != null && customerLoanIds.Any())
            {
                // 3. Fetch ALL payments that belong to any of the customer's loans.
                // We include related data to make the table descriptive.
                var allPayments = await _context.LoanPayments
                    .Include(p => p.LoanApplication)
                        .ThenInclude(la => la.LoanProduct)
                    .Where(p => customerLoanIds.Contains(p.LoanId)) // Filter by the customer's loan IDs
                    .OrderByDescending(p => p.PaymentDate) // Show the most recent payments first
                    .ToListAsync();

                // 4. Map the database entities to our ViewModel list.
                foreach (var payment in allPayments)
                {
                    viewModel.Payments.Add(new RecentPaymentItem
                    {
                        PaymentDate = payment.PaymentDate,
                        Description = $"Payment for {payment.LoanApplication.LoanProduct.ProductName}",
                        LoanNumber = payment.LoanApplication.LoanNumber, // Populate the new LoanNumber property
                        PaidAmount = payment.PaidAmount,
                        Status = payment.Status
                    });
                }
            }

            // 5. Pass the ViewModel to the new "AllPayments" view.
            return View(viewModel);
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
                // Ensure you pass necessary ViewData back if returning to the LoanApplication view
                ViewData["ShowLoanForm"] = true; // Or however you manage this
                return View("LoanApplication"); // Or the correct path to your apply loan view
            }

            var selectedLoanProduct = await _context.LoanProducts.FindAsync(loanProductId);
            if (selectedLoanProduct == null)
            {
                ModelState.AddModelError("loanProductId", "Selected loan product is invalid."); // More specific error
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;
                return View("LoanApplication");
            }

            // --- Input Validations ---
            if (loanAmount <= 0)
            {
                ModelState.AddModelError("loanAmount", "Loan amount must be a positive value.");
            }
            if (loanAmount < selectedLoanProduct.MinAmount)
            {
                ModelState.AddModelError("loanAmount", $"Loan amount cannot be less than {selectedLoanProduct.MinAmount:C}.");
            }
            if (loanAmount > selectedLoanProduct.MaxAmount)
            {
                ModelState.AddModelError("loanAmount", $"Loan amount cannot exceed {selectedLoanProduct.MaxAmount:C}.");
            }

            if (tenure <= 0)
            {
                ModelState.AddModelError("tenure", "Tenure must be a positive value (in months).");
            }
            if (tenure > selectedLoanProduct.Tenure) // Assuming selectedLoanProduct.Tenure is max tenure for product
            {
                ModelState.AddModelError("tenure", $"Tenure cannot exceed {selectedLoanProduct.Tenure} months for this product.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;
                // Pass back the submitted values if you want to repopulate the form
                // You might want to pass the model/viewModel back here
                return View("LoanApplication");
            }

            // --- EMI Calculation ---
            decimal principal = loanAmount;
            decimal annualInterestRatePercent = selectedLoanProduct.InterestRate;
            int tenureInMonths = tenure;
            decimal calculatedEmi;

            if (tenureInMonths <= 0)
            {
                // This case should ideally be caught by validation, but as a fallback:
                calculatedEmi = principal; // Or handle as an error, as tenure must be positive for EMI calc
            }
            else
            {
                if (annualInterestRatePercent == 0) // Zero interest loan
                {
                    calculatedEmi = principal / tenureInMonths;
                }
                else
                {
                    decimal monthlyInterestRate = annualInterestRatePercent / 12 / 100;
                    // Standard EMI formula: P * R * (1+R)^N / ((1+R)^N - 1)
                    // Need to use double for Math.Pow
                    double r = (double)monthlyInterestRate;
                    double p = (double)principal;
                    int n = tenureInMonths;

                    if (r == 0) // Handles cases where very small interest rates might become 0 after division/rounding before this check
                    {
                        calculatedEmi = principal / tenureInMonths;
                    }
                    else if (1 + r <= 0 && n % 1 != 0) // Safety for Math.Pow (highly unlikely with positive rates)
                    {
                        Console.WriteLine($"Warning: Math.Pow unstable condition for EMI calculation. Rate: {r}, Principal: {p}, Tenure: {n}. Falling back to simple division.");
                        calculatedEmi = principal / n;
                    }
                    else
                    {
                        double emiDouble = p * r * Math.Pow(1 + r, n) / (Math.Pow(1 + r, n) - 1);
                        if (double.IsNaN(emiDouble) || double.IsInfinity(emiDouble))
                        {
                            Console.WriteLine($"Warning: EMI calculation resulted in NaN or Infinity. Rate: {r}, Principal: {p}, Tenure: {n}. Falling back to simple division.");
                            calculatedEmi = principal / tenureInMonths; // Fallback for safety
                        }
                        else
                        {
                            calculatedEmi = (decimal)emiDouble;
                        }
                    }
                }
            }
            decimal finalCalculatedEmi = Math.Round(calculatedEmi, 2);
            Console.WriteLine($"ApplyForLoan POST (CustomerId: {customerId}): LoanAmount={loanAmount}, Rate={annualInterestRatePercent}, Tenure={tenureInMonths}, Calculated EMI={finalCalculatedEmi}");


            var loanApplication = new LoanApplication
            {
                CustomerId = customerId,
                LoanProductId = loanProductId,
                LoanAmount = loanAmount,
                ApplicationDate = DateTime.Now,
                ApprovalStatus = "Pending", // Initial status for new applications
                InterestRate = selectedLoanProduct.InterestRate,
                TenureMonths = tenure,
                LoanNumber = "APL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Example loan number

                // ** Assign the calculated EMI here **
                EMI = finalCalculatedEmi,

                // These are set upon approval/disbursement or during payment processing
                OutstandingBalance = 0, // Will be set to principal upon approval
                NextDueDate = null,
                LastPaymentDate = null,
                AmountDue = 0,          // Will be set to first EMI upon approval
                LoanStatus = "Pending", // Initial overall status; admin approval moves to "Active" (or "Pending Disbursement" then "Active")
                                        // Changed from "Pending Disbursement" to "Pending" for consistency, approval sets it to "Active"
                OverdueMonths = 0,
                CurrentOverdueAmount = 0
            };

            try
            {
                _context.LoanApplications.Add(loanApplication);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your loan application has been submitted successfully! EMI will be approximately " + finalCalculatedEmi.ToString("C");
                return RedirectToAction("LoanStatus"); // Assuming you have a LoanStatus view
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting loan application for CustomerId {customerId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                ModelState.AddModelError("", "An unexpected error occurred while submitting your application. Please try again.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;
                // Pass back the model with values for repopulation if you have a proper view model
                // For now, returning to "LoanApplication" view
                return View("LoanApplication", new { loanProductId, loanAmount, tenure }); // Pass back some values if needed by view
            }
        }

        [HttpGet]
        public async Task<IActionResult> CustomerStatements()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                // If not authenticated or not a customer, redirect to the login page.
                // It's good practice to also include a returnUrl if your login action supports it.
                return RedirectToAction("Login", "Account");
            }

            // 2. Get CustomerId from Claims: Retrieve the logged-in customer's ID.
            var customerIdClaim = User.FindFirstValue("CustomerId"); // "CustomerId" should match the claim type used during login.

            // Initialize the ViewModel. It's ALWAYS initialized to ensure the view never receives a null model,
            // which helps prevent NullReferenceExceptions in the Razor page.
            var viewModel = new CustomerStatementViewModel();

            // 3. Validate CustomerId Claim: Ensure the CustomerId claim was found and is a valid integer.
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                // If CustomerId is missing or invalid, set an error message and redirect.
                // Redirecting to Logout is one option; another might be a dedicated error page or back to login.
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            try
            {
                // 4. Fetch Customer: Retrieve customer details from the database.
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    // If no customer is found for the given ID, populate error message in ViewModel.
                    viewModel.ErrorMessage = $"Customer with ID {customerId} not found. Please ensure a valid customer ID is provided.";
                    // _logger?.LogWarning("Customer not found for Statement with Id: {CustomerId}", customerId); // Optional logging
                    return View(viewModel); // Return the view with the error message displayed.
                }

                // 5. Populate ViewModel with Customer Details
                viewModel.CustomerId = customer.CustomerId;
                viewModel.CustomerName = customer.Name;

                // 6. Fetch Loan Applications: Retrieve all loan applications for this customer.
                // Eagerly load related LoanProduct and Repayments to avoid N+1 query issues.
                var loanApplications = await _context.LoanApplications
                    .Where(la => la.CustomerId == customerId)
                    .Include(la => la.LoanProduct)  // Include product details for each loan
                    .Include(la => la.Repayments)   // Include repayment history for each loan
                    .ToListAsync();

                // 7. Populate LoanAccountsForSelection (for dropdown) and LoanStatements (detailed view for each loan)
                foreach (var app in loanApplications)
                {
                    // Populate the list for the loan selection dropdown.
                    viewModel.LoanAccountsForSelection.Add(new LoanAccountSelectItemViewModel
                    {
                        // LoanNumber should be a unique identifier for the loan account itself.
                        LoanIdValue = app.LoanNumber,
                        // Display text for the dropdown option.
                        LoanDisplayText = $"{app.LoanProduct?.ProductName ?? "N/A"} ({app.LoanNumber}) - {app.LoanAmount:C0}" // C0 format for currency with no decimals.
                    });

                    // Create a detailed statement view model for each loan application.
                    var loanDetail = new LoanStatementDetailViewModel
                    {
                        UniqueLoanIdentifier = app.LoanNumber, // Used to identify the div for this loan's details
                        ApplicationIdDisplay = app.LoanNumber, // Displaying LoanNumber as the Application ID in the view
                        ProductName = app.LoanProduct?.ProductName ?? "N/A", // Handle cases where LoanProduct might be null
                        LoanAmount = app.LoanAmount,
                        InterestRate = app.InterestRate, // Convert decimal rate (e.g., 0.075) to percentage (e.g., 7.5)
                        TenureMonths = app.TenureMonths,
                        ApplicationDate = app.ApplicationDate,
                        ApprovalStatus = app.ApprovalStatus, // This is the string status from LoanApplication model
                        OutstandingBalance = app.OutstandingBalance
                    };

                    // Populate repayment history for this specific loan, ordered by DueDate.
                    if (app.Repayments != null)
                    {
                        foreach (var repayment in app.Repayments.OrderBy(r => r.DueDate))
                        {
                            loanDetail.RepaymentHistory.Add(new RepaymentHistoryItemViewModel
                            {
                                RepaymentId = repayment.RepaymentId,
                                DueDate = repayment.DueDate,
                                AmountDue = repayment.AmountDue,
                                PaymentDate = repayment.PaymentDate, // This is nullable
                                PaymentStatus = repayment.PaymentStatus // This is the string status from Repayment model
                            });
                        }
                    }
                    viewModel.LoanStatements.Add(loanDetail);
                }

                // 8. Calculate Overall Summary Statistics
                if (loanApplications.Any()) // Proceed only if there are any loan applications
                {
                    // *** MODIFIED SECTION FOR ACCURATE SUMMARY CALCULATIONS ***
                    // Define the status strings from enums once for clarity and efficiency.
                    // Ensures that comparison is made against the correct string values (e.g., "ACTIVE").
                    string activeStatusString = LoanOverallStatus.ACTIVE.ToString();
                    string approvedStatusString = LoanApprovalStatus.APPROVED.ToString();
                    string closedStatusString = LoanOverallStatus.CLOSED.ToString();
                    string overdueStatusString = LoanOverallStatus.OVERDUE.ToString();

                    // Calculate TotalActiveLoans using case-insensitive comparison.
                    // Checks if LoanStatus is not null/empty before comparing.
                    viewModel.TotalActiveLoans = loanApplications
                        .Count(la => !string.IsNullOrEmpty(la.LoanStatus) &&
                                     la.LoanStatus.Equals(activeStatusString, StringComparison.OrdinalIgnoreCase));

                    // Calculate TotalAmountDisbursed using case-insensitive comparison.
                    // A loan is considered disbursed if it's Approved OR Active OR Closed.
                    viewModel.TotalAmountDisbursed = loanApplications
                        .Where(la =>
                            (!string.IsNullOrEmpty(la.ApprovalStatus) &&
                             la.ApprovalStatus.Equals(approvedStatusString, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(activeStatusString, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(closedStatusString, StringComparison.OrdinalIgnoreCase))
                        )
                        .Sum(la => la.LoanAmount);

                    // Calculate TotalOutstandingAmount using case-insensitive comparison.
                    // Outstanding amount is typically for loans that are Active OR Overdue.
                    viewModel.TotalOutstandingAmount = loanApplications
                        .Where(la =>
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(activeStatusString, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(overdueStatusString, StringComparison.OrdinalIgnoreCase))
                        )
                        .Sum(la => la.OutstandingBalance);
                }
                else
                {
                    // If the customer has no loan applications, explicitly set summary figures to 0.
                    // (Though int/decimal properties default to 0, explicit assignment is clearer).
                    viewModel.TotalActiveLoans = 0;
                    viewModel.TotalAmountDisbursed = 0;
                    viewModel.TotalOutstandingAmount = 0;
                }
            }
            catch (Exception ex)
            {
                // 9. Error Handling: Catch any unexpected exceptions during processing.
                // _logger?.LogError(ex, "Error retrieving statement for CustomerId {CustomerId}", customerId); // Optional logging
                viewModel.ErrorMessage = "An unexpected error occurred while retrieving your statement data. Please try again later or contact support.";

                // Optionally clear any partially populated data to present a clean error state.
                viewModel.LoanStatements?.Clear();
                viewModel.LoanAccountsForSelection?.Clear();

                // Ensure summary statistics are also zeroed out in case of an error.
                viewModel.TotalActiveLoans = 0;
                viewModel.TotalAmountDisbursed = 0;
                viewModel.TotalOutstandingAmount = 0;
            }

            // 10. Return View: Pass the populated (or error-state) ViewModel to the Razor view.
            return View(viewModel); // Always return the viewModel, which is never null.
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
        [Authorize] // Ensures only logged-in users can update their profile
        public async Task<IActionResult> CustomerUpdate()
        {
            // 1. Get the ID of the currently logged-in user.
            // The User's ID is stored in a "Claim" after they log in.
            // We retrieve it here to ensure users can only edit their own profile.
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int customerId))
            {
                // If we can't find the user's ID, it's a bad request or they are not properly logged in.
                return Unauthorized("User ID is not available or invalid.");
            }

            // 2. Find the customer in the database using their ID.
            // We use 'FindAsync' for an efficient lookup by primary key.
            var customer = await _context.Customers.FindAsync(customerId);

            // 3. Check if the customer exists.
            if (customer == null)
            {
                // If no customer is found with that ID, return a "Not Found" error.
                return NotFound();
            }

            // 4. Map the data from the database model (Customer) to the ViewModel (CustomerUpdateViewModel).
            // This is a crucial step. We only expose the data needed for the view, not the entire database object
            // (e.g., we don't send the PasswordHash to the client).
            var viewModel = new CustomerUpdateViewModel
            {
                CustomerId = customer.CustomerId,
                Name = customer.Name,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address
            };

            // 5. Return the 'CustomerUpdate' view, passing the populated ViewModel to it.
            // The view will use this ViewModel to pre-fill the form fields with the customer's current information.
            return View(viewModel);
        }
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerUpdate(CustomerUpdateViewModel model)
        {
            // 1. Check if the submitted data is valid.
            // ModelState.IsValid checks for validation rules defined in the ViewModel (e.g., [Required], [EmailAddress]).
            // If the model is not valid, the method will redisplay the form with validation error messages.
            if (ModelState.IsValid)
            {
                // 2. Fetch the original customer record from the database.
                // It's critical to retrieve the entity from the DB first to ensure we are updating a valid, existing record.
                // We use the CustomerId from the submitted model to find the correct record.
                var customerToUpdate = await _context.Customers.FindAsync(model.CustomerId);

                if (customerToUpdate == null)
                {
                    // If, for some reason, the customer doesn't exist, return a "Not Found" error.
                    return NotFound();
                }

                // Optional: Security check to ensure the logged-in user is the one they are trying to update.
                var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (customerToUpdate.CustomerId.ToString() != loggedInUserId)
                {
                    return Forbid(); // Or Unauthorized()
                }

                // 3. Update the properties of the retrieved database entity with the new values from the ViewModel.
                // We are deliberately NOT updating sensitive fields like PasswordHash or AccountNumber here.
                customerToUpdate.Name = model.Name;
                customerToUpdate.Email = model.Email;
                customerToUpdate.PhoneNumber = model.PhoneNumber;
                customerToUpdate.Address = model.Address;

                try
                {
                    // 4. Save the changes to the database.
                    // _context.Update(customerToUpdate) marks the entity as modified.
                    // _context.SaveChangesAsync() commits the changes to the database in a single transaction.
                    _context.Update(customerToUpdate);
                    await _context.SaveChangesAsync();

                    // (Optional) Add a success message to display on the next page.
                    TempData["SuccessMessage"] = "Your profile has been updated successfully!";

                    // 5. Redirect the user to a confirmation or details page upon successful update.
                    // RedirectToAction is used to prevent form re-submission if the user refreshes the page.
                    return RedirectToAction("CustomerDetails", "Customer");
                }
                catch (DbUpdateConcurrencyException)
                {
                    // This catch block handles rare cases where the record was modified by someone else
                    // between the time we loaded it and the time we tried to save it.
                    ModelState.AddModelError("", "The record you attempted to edit "
                        + "was modified by another user after you got the original value. "
                        + "Your edit operation was canceled.");
                    return View(model);
                }
            }

            // 6. If ModelState is invalid, return the view with the submitted model.
            // This ensures the form is redisplayed with the user's entered data and the corresponding validation error messages.
            return View(model);
        }

        // This action displays the list of accepted loans and simulates their disbursement
        // GET: /Customer/MakePayment/{loanApplicationId}
        // Displays the payment page for a specific loan, ensuring it belongs to the authenticated customer.
        [HttpGet]
        public async Task<IActionResult> MakePayment(int loanApplicationId)
        {
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Authentication error: Unable to identify customer.";
                return RedirectToAction("Login", "Account");
            }

            var loanApplication = await _context.LoanApplications
                                        .Include(la => la.LoanProduct)
                                        .FirstOrDefaultAsync(la => la.ApplicationId == loanApplicationId && la.CustomerId == customerId);

            if (loanApplication == null)
            {
                TempData["ErrorMessage"] = "Loan application not found or you do not have permission to access it.";
                return RedirectToAction("AcceptedLoans");
            }

            // Initialize ViewBag properties
            ViewBag.ShowPaymentForm = false; // Default to not showing the form
            ViewBag.PaymentButtonText = "Make Payment";
            ViewBag.PaymentFormDisabledMessage = "";
            ViewBag.DisplayLoanStatus = loanApplication.LoanStatus;
            ViewBag.DisplayAmountDue = loanApplication.AmountDue;
            ViewBag.DisplayOverdueMonths = loanApplication.OverdueMonths;
            ViewBag.DisplayCurrentOverdueAmount = loanApplication.CurrentOverdueAmount;

            if (loanApplication.LoanStatus == "Closed")
            {
                ViewBag.NoPaymentDueMessage = "This loan is fully paid and closed.";
            }
            else if (loanApplication.LoanStatus == "Pending Disbursement" || loanApplication.LoanStatus == "Pending")
            {
                ViewBag.NoPaymentDueMessage = "This loan is not yet active for payments.";
            }
            else // Loan is "Active" or "Overdue" from DB
            {
                DateTime today = DateTime.Now.Date;
                bool isEffectivelyOverdue = false;
                decimal effectiveOverdueAmountTotal = 0;
                int effectiveOverdueMonthsCount = 0;

                // Check for any past due PENDING installments to determine effective overdue status for display
                var pastDueRepayments = await _context.Repayments
                    .Where(r => r.ApplicationId == loanApplication.ApplicationId &&
                                 r.PaymentStatus == "PENDING" &&
                                 r.DueDate.Date < today)
                    .OrderBy(r => r.DueDate)
                    .ToListAsync();

                if (pastDueRepayments.Any())
                {
                    isEffectivelyOverdue = true;
                    ViewBag.DisplayLoanStatus = "Overdue"; // Override display status if not already set in DB
                    effectiveOverdueMonthsCount = pastDueRepayments.Count;
                    effectiveOverdueAmountTotal = pastDueRepayments.Sum(r => r.AmountDue);

                    ViewBag.DisplayOverdueMonths = effectiveOverdueMonthsCount;
                    ViewBag.DisplayCurrentOverdueAmount = effectiveOverdueAmountTotal;
                    ViewBag.DisplayAmountDue = effectiveOverdueAmountTotal; // Initially, amount due is total past due

                    // Add current month's EMI to DisplayAmountDue if it's also due and not part of pastDueRepayments
                    if (loanApplication.NextDueDate.HasValue && loanApplication.NextDueDate.Value.Date >= today)
                    {
                        var currentInstallment = await _context.Repayments
                            .FirstOrDefaultAsync(r => r.ApplicationId == loanApplication.ApplicationId &&
                                                        r.PaymentStatus == "PENDING" &&
                                                        r.DueDate.Date == loanApplication.NextDueDate.Value.Date);
                        if (currentInstallment != null)
                        {
                            ViewBag.DisplayAmountDue = effectiveOverdueAmountTotal + currentInstallment.AmountDue;
                        }
                    }
                }
                else
                {
                    // No past due installments, use DB values for overdue (should be 0 if no past dues)
                    // And set DisplayAmountDue to the current NextDueDate's amount from LoanApplication
                    ViewBag.DisplayAmountDue = loanApplication.AmountDue;
                    if (loanApplication.LoanStatus == "Overdue" && loanApplication.CurrentOverdueAmount > 0)
                    {
                        // This handles if DB says overdue but our check found no specific past due Repayment items.
                        // Could be an edge case or if CurrentOverdueAmount is a penalty. Prioritize it.
                        ViewBag.DisplayAmountDue = loanApplication.CurrentOverdueAmount;
                    }
                }


                // --- Logic for enabling/disabling payment form based on current NextDueDate ---
                if (loanApplication.NextDueDate.HasValue)
                {
                    DateTime nextDueDateValue = loanApplication.NextDueDate.Value.Date;
                    var repaymentForNextDueDate = await _context.Repayments
                        .FirstOrDefaultAsync(r => r.ApplicationId == loanApplication.ApplicationId &&
                                             r.DueDate.Date == nextDueDateValue);

                    if (repaymentForNextDueDate != null && repaymentForNextDueDate.PaymentStatus == "PENDING")
                    {
                        // An unpaid installment exists for the NextDueDate
                        // Allow payment if it's overdue OR if the current month has "arrived" for the due date
                        if (isEffectivelyOverdue || (today.Year == nextDueDateValue.Year && today.Month == nextDueDateValue.Month) || today > nextDueDateValue)
                        {
                            ViewBag.ShowPaymentForm = true;
                        }
                        else // Due date is in a future month, and not yet "that month"
                        {
                            ViewBag.ShowPaymentForm = false;
                            ViewBag.PaymentFormDisabledMessage = $"Next payment for {nextDueDateValue:MMMM d, yyyy} is scheduled. Payment option will be available from {new DateTime(nextDueDateValue.Year, nextDueDateValue.Month, 1):MMMM d, yyyy}.";
                        }
                    }
                    else if (repaymentForNextDueDate != null && repaymentForNextDueDate.PaymentStatus == "COMPLETED")
                    {
                        // The installment for the current NextDueDate is already paid.
                        // ProcessPayment should have advanced NextDueDate. If it hasn't, this is a state to show.
                        ViewBag.ShowPaymentForm = false;
                        ViewBag.PaymentFormDisabledMessage = $"Installment for {nextDueDateValue:MMMM d, yyyy} has been paid. The system will update to the next due date shortly.";
                        // You might want to query for the *actual* next pending repayment to display its details.
                        var actualNextPending = await _context.Repayments
                                                   .Where(r => r.ApplicationId == loanApplication.ApplicationId && r.PaymentStatus == "PENDING")
                                                   .OrderBy(r => r.DueDate)
                                                   .FirstOrDefaultAsync();
                        if (actualNextPending != null)
                        {
                            ViewBag.PaymentFormDisabledMessage += $" Next payment is due on {actualNextPending.DueDate:MMMM d, yyyy}.";
                        }
                        else
                        {
                            ViewBag.PaymentFormDisabledMessage = "All scheduled payments have been made or the loan is being finalized.";
                            // If all repayments are completed, the loan should ideally be 'Closed'.
                            // This indicates a potential need for ProcessPayment to fully close the loan.
                        }

                    }
                    else if (repaymentForNextDueDate == null && loanApplication.OutstandingBalance > 0)
                    {
                        // NextDueDate is set, but no matching Repayment schedule found (data integrity issue or end of schedule with balance)
                        ViewBag.ShowPaymentForm = false; // Default to false, as it's an unusual state
                        ViewBag.PaymentFormDisabledMessage = "Payment schedule details for the upcoming due date are currently inconsistent. Please contact support.";
                        // If outstanding balance is > 0 but no schedule, could allow a custom payment amount to clear if desired.
                        // For now, disable standard EMI payment.
                    }
                }
                else if (isEffectivelyOverdue) // Overdue, but NextDueDate on LoanApplication is null (should ideally point to oldest overdue)
                {
                    ViewBag.ShowPaymentForm = true; // Allow payment to clear dues
                }
                else if (loanApplication.OutstandingBalance > 0) // Active, no NextDueDate, but has balance (should have a schedule or be an error)
                {
                    ViewBag.PaymentFormDisabledMessage = "Loan is active but has no upcoming due date. Please contact support.";
                }


                // If loan is overdue, the payment form should definitely be shown to allow clearing dues
                if (ViewBag.DisplayLoanStatus == "Overdue" && ViewBag.DisplayCurrentOverdueAmount > 0)
                {
                    ViewBag.ShowPaymentForm = true;
                    ViewBag.PaymentFormDisabledMessage = ""; // Clear any previous disable message
                }
            }

            return View("~/Views/Customer/MakePayment.cshtml", loanApplication);
        }

        // POST: /Customer/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int loanId, decimal paidAmount, string paymentMethod)
        {
            // IMPORTANT: Validate that loanId belongs to the currently authenticated customer.
            // Example using claims:
            var customerIdClaim = User.FindFirst("CustomerId"); // Ensure "CustomerId" is the correct claim type
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                // If you're using ASP.NET Core Identity, User.Identity.Name might give username,
                // then you'd fetch customer ID from your users table.
                // Or, a common approach is to store CustomerId as a claim upon login.
                return Json(new { success = false, message = "User not authenticated or CustomerId not found in claims." });
            }

            if (paidAmount <= 0)
            {
                return Json(new { success = false, message = "Payment amount must be positive." });
            }

            var loanApplication = await _context.LoanApplications
                                                .Include(la => la.Repayments.Where(r => r.PaymentStatus == "PENDING").OrderBy(r => r.DueDate)) // Eager load pending repayments
                                                .FirstOrDefaultAsync(la => la.ApplicationId == loanId && la.CustomerId == currentCustomerId); // Enforce customer ownership

            if (loanApplication == null)
            {
                return Json(new { success = false, message = "Loan application not found or does not belong to the current user." });
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
                TransactionId = $"MOCKTRX{DateTime.Now.Ticks}", // Placeholder: Generate a real transaction ID from payment gateway
                Status = "Success" // Placeholder: Set based on actual payment gateway response
            };
            _context.LoanPayments.Add(payment);
            // Ensure LoanPayment.LoanId ForeignKey attribute correctly maps to LoanApplication.ApplicationId in your model.

            // --- 2. Apply Payment to Repayments and Update Loan Application ---
            decimal remainingAmountToAllocate = paidAmount;
            DateTime today = DateTime.Now.Date;

            // Handle overdue payments first
            // Check based on in-memory loaded repayments or explicitly query if status might have changed externally.
            // The eager-loaded 'loanApplication.Repayments' should only contain 'PENDING' ones.
            if (loanApplication.LoanStatus == "Overdue" ||
                loanApplication.Repayments.Any(r => r.DueDate.Date < today)) // No need to check r.PaymentStatus == "PENDING" if already filtered by Include
            {
                if (loanApplication.LoanStatus != "Overdue") loanApplication.LoanStatus = "Overdue"; // Correct status if needed

                var pendingOverdueRepayments = loanApplication.Repayments
                                                    .Where(r => r.DueDate.Date < today) // Already filtered for PENDING by Include, ordered by DueDate
                                                    .OrderBy(r => r.DueDate) // Re-order just to be certain if Include's order isn't guaranteed post-load modifications
                                                    .ToList();

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
                    repayment.PaymentStatus = "COMPLETED"; // EF Core tracks this change
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
            }

            // Apply to current/future EMIs if loan is Active or dues are now clear, and funds remain
            // Check if there are NO PENDING overdue repayments in the *in-memory collection* after the above processing.
            if ((loanApplication.LoanStatus == "Active" || !loanApplication.Repayments.Any(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today))
                && remainingAmountToAllocate > 0)
            {
                // Dues are clear, or loan was already active. Apply to current/future.
                // Fetch from the in-memory collection, which should reflect any updates from the overdue section.
                var nextPendingRepayments = loanApplication.Repayments
                                            .Where(r => r.PaymentStatus == "PENDING") // Still pending after potential overdue payments
                                            .OrderBy(r => r.DueDate)
                                            .ToList();

                foreach (var repayment in nextPendingRepayments)
                {
                    if (remainingAmountToAllocate <= 0) break;

                    decimal amountToApplyToThisRepayment = Math.Min(remainingAmountToAllocate, repayment.AmountDue);
                    decimal interestForThisEmiPeriod = CalculateInterestForPeriod(loanApplication.OutstandingBalance, loanApplication.InterestRate);
                    decimal principalFromThisEmi = Math.Max(0, amountToApplyToThisRepayment - interestForThisEmiPeriod);
                    principalFromThisEmi = Math.Min(principalFromThisEmi, loanApplication.OutstandingBalance);

                    loanApplication.OutstandingBalance -= principalFromThisEmi;
                    repayment.PaymentDate = DateTime.Now;
                    repayment.PaymentStatus = "COMPLETED"; // Mark as completed
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
            }

            if (loanApplication.OutstandingBalance < 0.01m && loanApplication.OutstandingBalance > -0.01m) // Handle potential floating point inaccuracies
            {
                loanApplication.OutstandingBalance = 0;
            }
            loanApplication.LastPaymentDate = DateTime.Now;

            // INSIDE ProcessPayment method, replacing the "FINAL STATE CALCULATION" block

            // --- FINAL STATE CALCULATION for LoanApplication ---
            // Ensure 'today' is defined in this scope

            // Explicitly get the current state of all Repayment entities for this loan
            // from EF Core's local change tracker. This reflects all in-memory changes.
            var allTrackedRepaymentsForThisLoan = _context.ChangeTracker.Entries<Repayment>()
                .Where(e => e.Entity.ApplicationId == loanApplication.ApplicationId)
                .Select(e => e.Entity)
                .ToList();

            // Log the state of these tracked repayments
            System.Diagnostics.Debug.WriteLine($"--- Repayment States from ChangeTracker (LoanID: {loanApplication.ApplicationId}) BEFORE final LA state calc ---");
            if (allTrackedRepaymentsForThisLoan.Any())
            {
                foreach (var rep in allTrackedRepaymentsForThisLoan.OrderBy(r => r.DueDate))
                {
                    System.Diagnostics.Debug.WriteLine($"ChangeTracker Source: RepID: {rep.RepaymentId}, Due: {rep.DueDate.ToShortDateString()}, Amt: {rep.AmountDue}, Status: {rep.PaymentStatus}, PayDate: {rep.PaymentDate?.ToShortDateString() ?? "N/A"}");
                }
            }
            else
            {
                // Fallback if change tracker isn't providing them for some reason (e.g. if they were re-fetched without tracking)
                // This path indicates a deeper issue if hit, as Include should make them tracked.
                allTrackedRepaymentsForThisLoan = loanApplication.Repayments?.ToList() ?? new List<Repayment>();
                System.Diagnostics.Debug.WriteLine($"Warning/Info: Using loanApplication.Repayments navigation property as fallback for final calc. Count: {allTrackedRepaymentsForThisLoan.Count}");
                // Log these too if fallback is used
                foreach (var rep in allTrackedRepaymentsForThisLoan.OrderBy(r => r.DueDate))
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback Source: RepID: {rep.RepaymentId}, Due: {rep.DueDate.ToShortDateString()}, Amt: {rep.AmountDue}, Status: {rep.PaymentStatus}, PayDate: {rep.PaymentDate?.ToShortDateString() ?? "N/A"}");
                }
            }
            System.Diagnostics.Debug.WriteLine("--- End Repayment States from ChangeTracker ---");


            var finalPastDueRepayments = allTrackedRepaymentsForThisLoan // Use this refreshed list
                .Where(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today)
                .ToList();

            var nextUpcomingPendingRepayment = allTrackedRepaymentsForThisLoan // Use this refreshed list
                .Where(r => r.PaymentStatus == "PENDING")
                .OrderBy(r => r.DueDate)
                .FirstOrDefault();

            System.Diagnostics.Debug.WriteLine($"Final Calc - From Tracked Repayments: Found {finalPastDueRepayments.Count} PENDING past due. Next upcoming PENDING: {nextUpcomingPendingRepayment?.DueDate.ToShortDateString() ?? "None"} (RepaymentID: {nextUpcomingPendingRepayment?.RepaymentId}, Status: {nextUpcomingPendingRepayment?.PaymentStatus})");


            if (loanApplication.OutstandingBalance <= 0.01m && !finalPastDueRepayments.Any() && nextUpcomingPendingRepayment == null)
            {
                loanApplication.LoanStatus = "Closed";
                loanApplication.OutstandingBalance = 0;
                loanApplication.NextDueDate = null;
                loanApplication.AmountDue = 0;
                loanApplication.CurrentOverdueAmount = 0;
                loanApplication.OverdueMonths = 0;
                System.Diagnostics.Debug.WriteLine($"LA State set to: Closed");
            }
            else if (finalPastDueRepayments.Any())
            {
                loanApplication.LoanStatus = "Overdue";
                loanApplication.OverdueMonths = finalPastDueRepayments.Count;
                loanApplication.CurrentOverdueAmount = finalPastDueRepayments.Sum(r => r.AmountDue);

                loanApplication.AmountDue = loanApplication.CurrentOverdueAmount;
                if (nextUpcomingPendingRepayment != null &&
                    !finalPastDueRepayments.Any(pr => pr.RepaymentId == nextUpcomingPendingRepayment.RepaymentId) && // Ensure it's not also a past due one
                    nextUpcomingPendingRepayment.DueDate.Date >= today) // And it's current or future
                {
                    loanApplication.AmountDue += nextUpcomingPendingRepayment.AmountDue;
                }
                loanApplication.NextDueDate = nextUpcomingPendingRepayment?.DueDate; // Should be the oldest overall PENDING
                System.Diagnostics.Debug.WriteLine($"LA State set in OVERDUE block: Status='{loanApplication.LoanStatus}', OverdueMonths='{loanApplication.OverdueMonths}', CurrentOverdueAmount='{loanApplication.CurrentOverdueAmount}', NextDueDate='{loanApplication.NextDueDate?.ToShortDateString()}', Calc AmountDue='{loanApplication.AmountDue}'");
            }
            else // No past dues, so loan is Active (if not closed)
            {
                loanApplication.LoanStatus = "Active";
                loanApplication.OverdueMonths = 0;
                loanApplication.CurrentOverdueAmount = 0m;
                loanApplication.AmountDue = nextUpcomingPendingRepayment?.AmountDue ?? 0;
                loanApplication.NextDueDate = nextUpcomingPendingRepayment?.DueDate;
                System.Diagnostics.Debug.WriteLine($"LA State set in ACTIVE block: Status='{loanApplication.LoanStatus}', OverdueMonths='{loanApplication.OverdueMonths}', CurrentOverdueAmount='{loanApplication.CurrentOverdueAmount}', NextDueDate='{loanApplication.NextDueDate?.ToShortDateString()}', Calc AmountDue='{loanApplication.AmountDue}'");
            }

            // Ensure OutstandingBalance isn't negative after all calculations
            if (loanApplication.OutstandingBalance < 0) loanApplication.OutstandingBalance = 0;

            // --- LOG STATE BEFORE SAVE --- (Keep this log)
            System.Diagnostics.Debug.WriteLine($"ProcessPayment - BEFORE Save - LoanID: {loanApplication.ApplicationId}, Status: {loanApplication.LoanStatus}, OB: {loanApplication.OutstandingBalance}, NextDue: {loanApplication.NextDueDate}, AmtDue: {loanApplication.AmountDue}, OverdueAmt: {loanApplication.CurrentOverdueAmount}, OverdueMonths: {loanApplication.OverdueMonths}");
           //PrintRepaymentStates(allTrackedRepaymentsForThisLoan, "Repayments from ChangeTracker/Fallback BEFORE Save"); // Log this collection

            // ... rest of your try/catch for SaveChangesAsync and JSON response ...

            try
            {
                await _context.SaveChangesAsync(); // Save all changes to LoanApplication, Repayments, and LoanPayments
                return Json(new
                {
                    success = true,
                    message = $"Payment of INR {paidAmount:N2} processed successfully.",
                    loanStatus = loanApplication.LoanStatus,
                    outstandingBalance = loanApplication.OutstandingBalance,
                    nextDueDate = loanApplication.NextDueDate?.ToString("yyyy-MM-dd"), // Format for JS consistency
                    amountDue = loanApplication.AmountDue, // This is the crucial total amount currently payable
                    currentOverdueAmount = loanApplication.CurrentOverdueAmount,
                    overdueMonths = loanApplication.OverdueMonths,
                    emi = loanApplication.EMI // Useful for defaulting payment amount if loan becomes Active
                });
            }
            catch (DbUpdateException ex) // More specific exception
            {
                // Log ex for detailed error diagnosis (inner exception often has more details)
                Console.WriteLine($"Error processing payment for LoanId {loanId}: {ex.Message} {ex.InnerException?.Message} {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving the payment details. Please try again." });
            }
            catch (Exception ex)
            {
                // Log ex for detailed error diagnosis
                Console.WriteLine($"Error processing payment for LoanId {loanId}: {ex.Message} {ex.StackTrace}");
                return Json(new { success = false, message = "An unexpected error occurred while processing the payment." });
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
            foreach (var loan in approvedLoans)
            {
                Console.WriteLine($"AcceptedLoans Action: LoanApplicationId = {loan.ApplicationId}, ApprovalStatus = {loan.ApprovalStatus}, CustomerId = {loan.CustomerId}");
            }
            return View(approvedLoans); // Pass the list of loans to an AcceptedLoans.cshtml view
        }

        // GET: /Customer/Statement/{customerId}
    }
}