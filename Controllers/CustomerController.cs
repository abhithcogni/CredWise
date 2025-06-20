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
using CredWise_Trail.Models.ViewModels;

namespace CredWise_Trail.Controllers
{
    public class CustomerController : Controller
    {
        private readonly BankLoanManagementDbContext _context;

        public CustomerController(BankLoanManagementDbContext context)
        {
            _context = context;
        }
        public IActionResult RequestSupport()
        {
            return View();
        }

        public async Task<IActionResult> CustomerDashboard()
        {
            //User is a property of the Controller class that gives us access to the current user. It contains information about the user's identity and claims.
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            //out is used to take values when we have 2 return values. Since TryParse gives a bool and int as return type, we use out to store both.
            var customerIdClaim = User.FindFirstValue("CustomerId");
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            var viewModel = new CustomerDashboardViewModel();

            //Eager Loading
            var relevantLoans = await _context.LoanApplications
                .Where(l => l.CustomerId == customerId &&
                            (l.LoanStatus == "Active" || l.LoanStatus == "Overdue")) 
                .ToListAsync();

            if (relevantLoans != null && relevantLoans.Any())
            {
                viewModel.HasActiveLoans = true; 
                                                 
                viewModel.ActiveLoanCount = relevantLoans.Count; 

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
                    viewModel.OverallProgressPercentage = 0;
                }


                var relevantLoanIds = relevantLoans.Select(l => l.ApplicationId).ToList();

                //Chained Eager Loading
                var recentPayments = await _context.LoanPayments
                    .Include(p => p.LoanApplication)      
                        .ThenInclude(la => la.LoanProduct) 
                    .Where(p => relevantLoanIds.Contains(p.LoanId)) 
                    .OrderByDescending(p => p.PaymentDate)
                    .Take(5)
                    .ToListAsync();

                foreach (var payment in recentPayments)
                {
                    viewModel.RecentPayments.Add(new RecentPaymentItem
                    {
                        PaymentDate = payment.PaymentDate,
                        Description = $"Payment for {payment.LoanApplication.LoanProduct?.ProductName ?? "Loan"} ({payment.LoanApplication.LoanNumber})",
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

        public async Task<IActionResult> AllPayments()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirstValue("CustomerId"); 
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account"); 
            }
            var viewModel = new AllPaymentsViewModel();

            var customerLoanIds = await _context.LoanApplications
                .Where(l => l.CustomerId == customerId)
                .Select(l => l.ApplicationId)
                .ToListAsync();

            if (customerLoanIds != null && customerLoanIds.Any())
            {
                var allPayments = await _context.LoanPayments
                    .Include(p => p.LoanApplication)
                        .ThenInclude(la => la.LoanProduct)
                    .Where(p => customerLoanIds.Contains(p.LoanId)) 
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                foreach (var payment in allPayments)
                {
                    viewModel.Payments.Add(new RecentPaymentItem
                    {
                        PaymentDate = payment.PaymentDate,
                        Description = $"Payment for {payment.LoanApplication.LoanProduct.ProductName}",
                        LoanNumber = payment.LoanApplication.LoanNumber,
                        PaidAmount = payment.PaidAmount,
                        Status = payment.Status
                    });
                }
            }

            return View(viewModel);
        }

        [HttpGet]

        public async Task<IActionResult> LoanApplication()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirstValue("CustomerId"); // Using your existing claim type
            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
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
                        var loanProducts = await _context.LoanProducts.ToListAsync();
                        ViewBag.LoanProducts = loanProducts;

                        // *** NEW LOGIC STARTS HERE ***
                        // Find loan products the customer already has an active or pending application for.
                        // This code is robust and handles duplicate LoanProductIds gracefully
                        var existingLoans = await _context.LoanApplications
                            .Where(la => la.CustomerId == customerId && la.LoanProductId.HasValue &&
                                            (la.ApprovalStatus == "Pending" || la.LoanStatus == "Active" || la.LoanStatus == "Overdue"))
                            .GroupBy(la => la.LoanProductId.Value) // 1. Group by the key that was causing duplicates
                            .Select(g => g.OrderByDescending(la => la.ApplicationDate).First()) // 2. From each group, select only the most recent application
                            .ToDictionaryAsync(
                                la => la.LoanProductId.Value, // Now this key is guaranteed to be unique
                                la => la.ApprovalStatus == "Pending" ? "Pending" : "Active"
                            ); // Simplified status for the alert

                        // Pass this dictionary to the view.
                        ViewData["RestrictedLoanProducts"] = existingLoans;
                        // *** NEW LOGIC ENDS HERE ***

                        break;
                    case "Pending":
                        TempData["WarningMessage"] = "Your KYC verification is currently pending. It must be approved before you can apply for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer");
                        ViewData["KycPageLinkText"] = "Check KYC Status / Upload Documents";
                        break;
                    case "Rejected":
                        TempData["ErrorMessage"] = "Your KYC verification was rejected. Please re-submit your KYC documents and get approval before applying for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer");
                        ViewData["KycPageLinkText"] = "Re-apply for KYC";
                        break;
                    default:
                        TempData["InfoMessage"] = $"Your KYC status is '{latestKyc.Status}'. Please ensure it is approved to apply for a loan.";
                        ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer");
                        ViewData["KycPageLinkText"] = "Review KYC Status";
                        break;
                }
            }
            else
            {
                ViewData["KycStatus"] = "Not Submitted";
                TempData["InfoMessage"] = "You need to complete and get your KYC verified before applying for a loan.";
                ViewData["KycPageLink"] = Url.Action("KYCUpload", "Customer");
                ViewData["KycPageLinkText"] = "Complete KYC Verification";
            }

            return View();
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
                ViewData["ShowLoanForm"] = true; 
                return View("LoanApplication"); 
            }

            var selectedLoanProduct = await _context.LoanProducts.FindAsync(loanProductId);
            if (selectedLoanProduct == null)
            {
                ModelState.AddModelError("loanProductId", "Selected loan product is invalid."); 
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;
                return View("LoanApplication");
            }

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
            if (tenure > selectedLoanProduct.Tenure)
            {
                ModelState.AddModelError("tenure", $"Tenure cannot exceed {selectedLoanProduct.Tenure} months for this product.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;
                return View("LoanApplication");
            }

            decimal principal = loanAmount;
            decimal annualInterestRatePercent = selectedLoanProduct.InterestRate;
            int tenureInMonths = tenure;
            decimal calculatedEmi;

            if (tenureInMonths <= 0)
            {
                calculatedEmi = principal;
            }
            else
            {
                if (annualInterestRatePercent == 0)
                {
                    calculatedEmi = principal / tenureInMonths;
                }
                else
                {
                    decimal monthlyInterestRate = annualInterestRatePercent / 12 / 100;
                    double r = (double)monthlyInterestRate;
                    double p = (double)principal;
                    int n = tenureInMonths;

                    if (r == 0)
                    {
                        calculatedEmi = principal / tenureInMonths;
                    }
                    else if (1 + r <= 0 && n % 1 != 0) 
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
                            calculatedEmi = principal / tenureInMonths; 
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
                ApprovalStatus = "Pending",
                InterestRate = selectedLoanProduct.InterestRate,
                TenureMonths = tenure,
                LoanNumber = "APL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),

                EMI = finalCalculatedEmi,
                LoanProductName= selectedLoanProduct.ProductName,

                OutstandingBalance = 0, 
                NextDueDate = null,
                LastPaymentDate = null,
                AmountDue = 0,          
                LoanStatus = "Pending", 
                OverdueMonths = 0,
                CurrentOverdueAmount = 0
            };

            try
            {
                _context.LoanApplications.Add(loanApplication);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your loan application has been submitted successfully! EMI will be approximately " + finalCalculatedEmi.ToString("C");
                return RedirectToAction("LoanStatus");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting loan application for CustomerId {customerId}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                ModelState.AddModelError("", "An unexpected error occurred while submitting your application. Please try again.");
                ViewBag.LoanProducts = await _context.LoanProducts.ToListAsync();
                ViewData["ShowLoanForm"] = true;

                return View("LoanApplication", new { loanProductId, loanAmount, tenure });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CustomerStatements()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                TempData["ErrorMessage"] = "Please log in as a customer to view your statements.";
                return RedirectToAction("Login", "Account");
            }

            var customerIdClaim = User.FindFirstValue("CustomerId"); 

            var viewModel = new CustomerStatementViewModel();

            if (string.IsNullOrEmpty(customerIdClaim) || !int.TryParse(customerIdClaim, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account"); 
            }

            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    viewModel.ErrorMessage = $"Customer with ID {customerId} not found. Please ensure a valid customer ID is provided.";
                    return View(viewModel);
                }

                viewModel.CustomerId = customer.CustomerId;
                viewModel.CustomerName = customer.Name;

                var loanApplications = await _context.LoanApplications
                    .Where(la => la.CustomerId == customerId)
                    .Include(la => la.LoanProduct) 
                    .Include(la => la.Repayments)
                    .ToListAsync();

                foreach (var app in loanApplications)
                {
                    viewModel.LoanAccountsForSelection.Add(new LoanAccountSelectItemViewModel
                    {
                        LoanIdValue = app.LoanNumber,
                        LoanDisplayText = $"{app.LoanProductName} " +
                        $"({app.LoanNumber}) - ₹{app.LoanAmount:N0}" 
                    });

                    var loanDetail = new LoanStatementDetailViewModel
                    {
                        UniqueLoanIdentifier = app.LoanNumber, 
                        ApplicationIdDisplay = app.LoanNumber, 
                        ProductName = app.LoanProductName, 
                        LoanAmount = app.LoanAmount,
                        InterestRate = app.InterestRate, 
                        TenureMonths = app.TenureMonths,
                        ApplicationDate = app.ApplicationDate,
                        ApprovalStatus = app.ApprovalStatus, 
                        LoanStatus = app.LoanStatus,        
                        OutstandingBalance = app.OutstandingBalance
                    };

                    if (app.Repayments != null)
                    {
                        foreach (var repayment in app.Repayments.OrderBy(r => r.DueDate))
                        {
                            loanDetail.RepaymentHistory.Add(new RepaymentHistoryItemViewModel
                            {
                                RepaymentId = repayment.RepaymentId,
                                DueDate = repayment.DueDate,
                                AmountDue = repayment.AmountDue,
                                PaymentDate = repayment.PaymentDate, 
                                PaymentStatus = repayment.PaymentStatus 
                            });
                        }
                    }
                    viewModel.LoanStatements.Add(loanDetail);
                }

                if (loanApplications.Any()) 
                {
                    string activeStatusString = LoanOverallStatus.ACTIVE.ToString();
                    string overdueStatusString = LoanOverallStatus.OVERDUE.ToString();
                    string approvedStatusString = LoanApprovalStatus.APPROVED.ToString();
                    string closedStatusString = LoanOverallStatus.CLOSED.ToString();

                    viewModel.TotalActiveLoans = loanApplications
                        .Count(la => !string.IsNullOrEmpty(la.LoanStatus) &&
                                     (la.LoanStatus.Equals(activeStatusString, StringComparison.OrdinalIgnoreCase) ||
                                      la.LoanStatus.Equals(overdueStatusString, StringComparison.OrdinalIgnoreCase)));

                    
                    viewModel.TotalAmountDisbursed = loanApplications
                        .Where(la =>
                            (!string.IsNullOrEmpty(la.ApprovalStatus) &&
                             la.ApprovalStatus.Equals(approvedStatusString, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(activeStatusString, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(la.LoanStatus) &&
                             la.LoanStatus.Equals(overdueStatusString, StringComparison.OrdinalIgnoreCase))
                        )
                        .Sum(la => la.LoanAmount);

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
                    viewModel.TotalActiveLoans = 0;
                    viewModel.TotalAmountDisbursed = 0;
                    viewModel.TotalOutstandingAmount = 0;
                }
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = "An unexpected error occurred while retrieving your statement data. Please try again later or contact support.";

                viewModel.LoanStatements?.Clear();
                viewModel.LoanAccountsForSelection?.Clear();

                viewModel.TotalActiveLoans = 0;
                viewModel.TotalAmountDisbursed = 0;
                viewModel.TotalOutstandingAmount = 0;
            }

            return View(viewModel);
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
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> KYCUpload()
        {

            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";
                return RedirectToAction("Logout", "Account");
            }

            var latestKyc = await _context.KycApprovals
                                        .Where(k => k.CustomerId == customerId)
                                        .OrderByDescending(k => k.SubmissionDate)
                                        .FirstOrDefaultAsync();

            var model = new KycUploadViewModel();

            if (latestKyc != null)
            {
                ViewData["CurrentKycStatus"] = latestKyc.Status;
                switch (latestKyc.Status)
                {
                    case "Approved":
                        ViewData["ShowForm"] = false;
                        TempData["InfoMessage"] = "Your KYC has already been approved. No further action is required.";
                        break;
                    case "Pending":
                        ViewData["ShowForm"] = true;
                        TempData["InfoMessage"] = "Your KYC submission is currently pending review. You can upload new documents if you wish to replace the previous submission.";
                        break;
                    case "Rejected":
                        ViewData["ShowForm"] = true;
                        TempData["WarningMessage"] = "Your previous KYC submission was rejected. Please review any feedback and upload the correct documents again.";
                        break;
                    default:
                        ViewData["ShowForm"] = true;
                        break;
                }
            }
            else
            {
                ViewData["ShowForm"] = true;
                ViewData["CurrentKycStatus"] = "Not Submitted"; 
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> KYCUpload(KycUploadViewModel model)

        {

            var customerIdClaim = User.FindFirst("CustomerId");

            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))

            {

                TempData["ErrorMessage"] = "Could not identify customer. Please log in again.";

                return RedirectToAction("Logout", "Account");

            }

            var existingKyc = await _context.KycApprovals

                                            .FirstOrDefaultAsync(k => k.CustomerId == customerId);

            if (existingKyc != null && existingKyc.Status == "Approved")

            {

                TempData["InfoMessage"] = "Your KYC is already approved. You cannot submit new documents.";

                return RedirectToAction("CustomerDashboard");

            }

            if (ModelState.IsValid)

            {

                string contentRootPath = Directory.GetCurrentDirectory();

                string uploadFolder = Path.Combine(contentRootPath, "kyc_documents");

                if (!Directory.Exists(uploadFolder))

                {

                    Directory.CreateDirectory(uploadFolder);

                }

                if (model.IdentityProofFile == null || model.IdentityProofFile.Length == 0)

                {

                    ModelState.AddModelError("IdentityProofFile", "Identity proof document is required.");

                    TempData["ErrorMessage"] = "Identity proof document is required.";

                    return View(model);

                }

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

                try

                {

                    string newIdentityFileName = $"{customerId}_{Guid.NewGuid()}_identity{fileExtension}";

                    string newFilePath = Path.Combine(uploadFolder, newIdentityFileName);

                    using (var fileStream = new FileStream(newFilePath, FileMode.Create))

                    {

                        await model.IdentityProofFile.CopyToAsync(fileStream);

                    }

                    if (existingKyc != null)

                    {

                        if (!string.IsNullOrEmpty(existingKyc.DocumentPath))

                        {

                            string oldFilePath = Path.Combine(uploadFolder, existingKyc.DocumentPath);

                            if (System.IO.File.Exists(oldFilePath))

                            {

                                System.IO.File.Delete(oldFilePath);

                            }

                        }

                        existingKyc.SubmissionDate = DateTime.UtcNow;

                        existingKyc.Status = "Pending";

                        existingKyc.DocumentPath = newIdentityFileName;

                        _context.KycApprovals.Update(existingKyc);

                        TempData["SuccessMessage"] = "Your updated KYC documents have been submitted successfully! Your verification is pending review.";

                    }

                    else

                    {

                        var newKycApproval = new KycApproval

                        {

                            CustomerId = customerId,

                            SubmissionDate = DateTime.UtcNow,

                            Status = "Pending",

                            DocumentPath = newIdentityFileName,

                        };

                        _context.KycApprovals.Add(newKycApproval);

                        TempData["SuccessMessage"] = "KYC documents uploaded successfully! Your verification is pending review.";

                    }

                    await _context.SaveChangesAsync();

                    return RedirectToAction("KYCUpload");

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

                foreach (var state in ModelState)

                {

                    foreach (var error in state.Value.Errors)

                    {

                        Console.WriteLine($"ModelState Error: {state.Key} - {error.ErrorMessage}");

                    }

                }

                TempData["ErrorMessage"] = "Please correct the errors below and try again.";

            }

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
        [Authorize]     
        public async Task<IActionResult> CustomerUpdate()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int customerId))
            {
                return Unauthorized("User ID is not available or invalid.");
            }
            var customer = await _context.Customers.FindAsync(customerId);

            if (customer == null)
            {
                return NotFound();
            }

            var viewModel = new CustomerUpdateViewModel
            {
                CustomerId = customer.CustomerId,
                Name = customer.Name,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address
            };

            return View(viewModel);
        }
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerUpdate(CustomerUpdateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var customerToUpdate = await _context.Customers.FindAsync(model.CustomerId);

                if (customerToUpdate == null)
                {
                    return NotFound();
                }

                var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (customerToUpdate.CustomerId.ToString() != loggedInUserId)
                {
                    return Forbid(); 
                }

                customerToUpdate.Name = model.Name;
                customerToUpdate.Email = model.Email;
                customerToUpdate.PhoneNumber = model.PhoneNumber;
                customerToUpdate.Address = model.Address;

                try
                {
                    _context.Update(customerToUpdate);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Your profile has been updated successfully!";

                    return RedirectToAction("CustomerDetails", "Customer");
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError("", "The record you attempted to edit "
                        + "was modified by another user after you got the original value. "
                        + "Your edit operation was canceled.");
                    return View(model);
                }
            }

            return View(model);
        }

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

            ViewBag.ShowPaymentForm = false; 
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
            else 
            {
                DateTime today = DateTime.Now.Date;
                bool isEffectivelyOverdue = false;
                decimal effectiveOverdueAmountTotal = 0;
                int effectiveOverdueMonthsCount = 0;

                var pastDueRepayments = await _context.Repayments
                    .Where(r => r.ApplicationId == loanApplication.ApplicationId &&
                                 r.PaymentStatus == "PENDING" &&
                                 r.DueDate.Date < today)
                    .OrderBy(r => r.DueDate)
                    .ToListAsync();

                if (pastDueRepayments.Any())
                {
                    isEffectivelyOverdue = true;
                    ViewBag.DisplayLoanStatus = "Overdue"; 
                    effectiveOverdueMonthsCount = pastDueRepayments.Count;
                    effectiveOverdueAmountTotal = pastDueRepayments.Sum(r => r.AmountDue);

                    ViewBag.DisplayOverdueMonths = effectiveOverdueMonthsCount;
                    ViewBag.DisplayCurrentOverdueAmount = effectiveOverdueAmountTotal;
                    ViewBag.DisplayAmountDue = effectiveOverdueAmountTotal; 

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
                    ViewBag.DisplayAmountDue = loanApplication.AmountDue;
                    if (loanApplication.LoanStatus == "Overdue" && loanApplication.CurrentOverdueAmount > 0)
                    {
                        
                        ViewBag.DisplayAmountDue = loanApplication.CurrentOverdueAmount;
                    }
                }

                if (loanApplication.NextDueDate.HasValue)
                {
                    DateTime nextDueDateValue = loanApplication.NextDueDate.Value.Date;
                    var repaymentForNextDueDate = await _context.Repayments
                        .FirstOrDefaultAsync(r => r.ApplicationId == loanApplication.ApplicationId &&
                                             r.DueDate.Date == nextDueDateValue);

                    if (repaymentForNextDueDate != null && repaymentForNextDueDate.PaymentStatus == "PENDING")
                    {
                        if (isEffectivelyOverdue || (today.Year == nextDueDateValue.Year && today.Month == nextDueDateValue.Month) || today > nextDueDateValue)
                        {
                            ViewBag.ShowPaymentForm = true;
                        }
                        else 
                        {
                            ViewBag.ShowPaymentForm = false;
                            ViewBag.PaymentFormDisabledMessage = $"Next payment for {nextDueDateValue:MMMM d, yyyy} is scheduled. Payment option will be available from {new DateTime(nextDueDateValue.Year, nextDueDateValue.Month, 1):MMMM d, yyyy}.";
                        }
                    }
                    else if (repaymentForNextDueDate != null && repaymentForNextDueDate.PaymentStatus == "COMPLETED")
                    {
                        ViewBag.ShowPaymentForm = false;
                        ViewBag.PaymentFormDisabledMessage = $"Installment for {nextDueDateValue:MMMM d, yyyy} has been paid. The system will update to the next due date shortly.";
                        
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
                            
                        }

                    }
                    else if (repaymentForNextDueDate == null && loanApplication.OutstandingBalance > 0)
                    {
                        ViewBag.ShowPaymentForm = false; 
                        ViewBag.PaymentFormDisabledMessage = "Payment schedule details for the upcoming due date are currently inconsistent. Please contact support.";
                        
                    }
                }
                else if (isEffectivelyOverdue)
                {
                    ViewBag.ShowPaymentForm = true; 
                }
                else if (loanApplication.OutstandingBalance > 0) 
                {
                    ViewBag.PaymentFormDisabledMessage = "Loan is active but has no upcoming due date. Please contact support.";
                }

                if (ViewBag.DisplayLoanStatus == "Overdue" && ViewBag.DisplayCurrentOverdueAmount > 0)
                {
                    ViewBag.ShowPaymentForm = true;
                    ViewBag.PaymentFormDisabledMessage = ""; 
                }
            }

            return View("~/Views/Customer/MakePayment.cshtml", loanApplication);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int loanId, decimal paidAmount, string paymentMethod)
        {
            var customerIdClaim = User.FindFirst("CustomerId"); 
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                return Json(new { success = false, message = "User not authenticated or CustomerId not found in claims." });
            }

            if (paidAmount <= 0)
            {
                return Json(new { success = false, message = "Payment amount must be positive." });
            }

            var loanApplication = await _context.LoanApplications
                                                .Include(la => la.Repayments.Where(r => r.PaymentStatus == "PENDING").OrderBy(r => r.DueDate))
                                                .FirstOrDefaultAsync(la => la.ApplicationId == loanId && la.CustomerId == currentCustomerId);

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
            var payment = new LoanPayment
            {
                LoanId = loanApplication.ApplicationId, 
                CustomerId = loanApplication.CustomerId,
                PaidAmount = paidAmount,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                TransactionId = $"MOCKTRX{DateTime.Now.Ticks}", 
                Status = "Success" 
            };
            _context.LoanPayments.Add(payment);

            decimal remainingAmountToAllocate = paidAmount;
            DateTime today = DateTime.Now.Date;

            if (loanApplication.LoanStatus == "Overdue" ||
                loanApplication.Repayments.Any(r => r.DueDate.Date < today))
            {
                if (loanApplication.LoanStatus != "Overdue") loanApplication.LoanStatus = "Overdue"; 

                var pendingOverdueRepayments = loanApplication.Repayments
                                                    .Where(r => r.DueDate.Date < today)
                                                    .OrderBy(r => r.DueDate) 
                                                    .ToList();

                foreach (var repayment in pendingOverdueRepayments)
                {
                    if (remainingAmountToAllocate <= 0) break;

                    decimal amountToApplyToThisRepayment = Math.Min(remainingAmountToAllocate, repayment.AmountDue);
                    decimal interestForThisEmiPeriod = CalculateInterestForPeriod(loanApplication.OutstandingBalance, loanApplication.InterestRate);
                    decimal principalFromThisEmi = Math.Max(0, amountToApplyToThisRepayment - interestForThisEmiPeriod);
                    principalFromThisEmi = Math.Min(principalFromThisEmi, loanApplication.OutstandingBalance); 

                    loanApplication.OutstandingBalance -= principalFromThisEmi;
                    repayment.PaymentDate = DateTime.Now;
                    repayment.PaymentStatus = "COMPLETED";
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
            }

            if ((loanApplication.LoanStatus == "Active" || !loanApplication.Repayments.Any(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today))
                && remainingAmountToAllocate > 0)
            {
                var nextPendingRepayments = loanApplication.Repayments
                                            .Where(r => r.PaymentStatus == "PENDING") 
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
                    repayment.PaymentStatus = "COMPLETED";
                    remainingAmountToAllocate -= amountToApplyToThisRepayment;
                }
            }

            if (loanApplication.OutstandingBalance < 0.01m && loanApplication.OutstandingBalance > -0.01m) 
            {
                loanApplication.OutstandingBalance = 0;
            }
            loanApplication.LastPaymentDate = DateTime.Now;

            var allTrackedRepaymentsForThisLoan = _context.ChangeTracker.Entries<Repayment>()
                .Where(e => e.Entity.ApplicationId == loanApplication.ApplicationId)
                .Select(e => e.Entity)
                .ToList();

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
                
                allTrackedRepaymentsForThisLoan = loanApplication.Repayments?.ToList() ?? new List<Repayment>();
                System.Diagnostics.Debug.WriteLine($"Warning/Info: Using loanApplication.Repayments navigation property as fallback for final calc. Count: {allTrackedRepaymentsForThisLoan.Count}");
                foreach (var rep in allTrackedRepaymentsForThisLoan.OrderBy(r => r.DueDate))
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback Source: RepID: {rep.RepaymentId}, Due: {rep.DueDate.ToShortDateString()}, Amt: {rep.AmountDue}, Status: {rep.PaymentStatus}, PayDate: {rep.PaymentDate?.ToShortDateString() ?? "N/A"}");
                }
            }
            System.Diagnostics.Debug.WriteLine("--- End Repayment States from ChangeTracker ---");


            var finalPastDueRepayments = allTrackedRepaymentsForThisLoan 
                .Where(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today)
                .ToList();

            var nextUpcomingPendingRepayment = allTrackedRepaymentsForThisLoan 
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
                    !finalPastDueRepayments.Any(pr => pr.RepaymentId == nextUpcomingPendingRepayment.RepaymentId) && 
                    nextUpcomingPendingRepayment.DueDate.Date >= today) 
                {
                    loanApplication.AmountDue += nextUpcomingPendingRepayment.AmountDue;
                }
                loanApplication.NextDueDate = nextUpcomingPendingRepayment?.DueDate; 
                System.Diagnostics.Debug.WriteLine($"LA State set in OVERDUE block: Status='{loanApplication.LoanStatus}', OverdueMonths='{loanApplication.OverdueMonths}', CurrentOverdueAmount='{loanApplication.CurrentOverdueAmount}', NextDueDate='{loanApplication.NextDueDate?.ToShortDateString()}', Calc AmountDue='{loanApplication.AmountDue}'");
            }
            else 
            {
                loanApplication.LoanStatus = "Active";
                loanApplication.OverdueMonths = 0;
                loanApplication.CurrentOverdueAmount = 0m;
                loanApplication.AmountDue = nextUpcomingPendingRepayment?.AmountDue ?? 0;
                loanApplication.NextDueDate = nextUpcomingPendingRepayment?.DueDate;
                System.Diagnostics.Debug.WriteLine($"LA State set in ACTIVE block: Status='{loanApplication.LoanStatus}', OverdueMonths='{loanApplication.OverdueMonths}', CurrentOverdueAmount='{loanApplication.CurrentOverdueAmount}', NextDueDate='{loanApplication.NextDueDate?.ToShortDateString()}', Calc AmountDue='{loanApplication.AmountDue}'");
            }

            if (loanApplication.OutstandingBalance < 0) loanApplication.OutstandingBalance = 0;

            System.Diagnostics.Debug.WriteLine($"ProcessPayment - BEFORE Save - LoanID: {loanApplication.ApplicationId}, Status: {loanApplication.LoanStatus}, OB: {loanApplication.OutstandingBalance}, NextDue: {loanApplication.NextDueDate}, AmtDue: {loanApplication.AmountDue}, OverdueAmt: {loanApplication.CurrentOverdueAmount}, OverdueMonths: {loanApplication.OverdueMonths}");

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
                    amountDue = loanApplication.AmountDue, 
                    currentOverdueAmount = loanApplication.CurrentOverdueAmount,
                    overdueMonths = loanApplication.OverdueMonths,
                    emi = loanApplication.EMI 
                });
            }
            catch (DbUpdateException ex) 
            {
                Console.WriteLine($"Error processing payment for LoanId {loanId}: {ex.Message} {ex.InnerException?.Message} {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while saving the payment details. Please try again." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing payment for LoanId {loanId}: {ex.Message} {ex.StackTrace}");
                return Json(new { success = false, message = "An unexpected error occurred while processing the payment." });
            }
        }

        private decimal CalculateInterestForPeriod(decimal currentOutstandingBalance, decimal annualInterestRatePercentage)
        {
            if (currentOutstandingBalance <= 0) return 0;
            decimal monthlyInterestRate = annualInterestRatePercentage / 12 / 100;
            return Math.Round(currentOutstandingBalance * monthlyInterestRate, 2);
        }
        public async Task<IActionResult> AcceptedLoans()
        {
            var customerIdClaim = User.FindFirst("CustomerId"); 
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Authentication error: Customer ID not found.";
                return RedirectToAction("Login", "Account"); 
            }

            var approvedLoans = await _context.LoanApplications
                                            .Include(l => l.LoanProduct) 
                                            .Where(l => l.CustomerId == customerId && l.ApprovalStatus == "Approved")
                                            .Where(l => l.LoanStatus != "Closed") 
                                            .ToListAsync();

            bool hasChanges = false;
            foreach (var loan in approvedLoans)
            {
                if (loan.LoanStatus == "Pending Disbursement") 
                {
                }
            }
            if (hasChanges)
            {
                await _context.SaveChangesAsync(); 
            }
            foreach (var loan in approvedLoans)
            {
                Console.WriteLine($"AcceptedLoans Action: LoanApplicationId = {loan.ApplicationId}, ApprovalStatus = {loan.ApprovalStatus}, CustomerId = {loan.CustomerId}");
            }
            return View(approvedLoans); 
        }
    }
}