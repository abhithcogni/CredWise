using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CredWise_Trail.Models;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Linq; // Added for LINQ operations

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

            var loanProducts = await _context.LoanProducts.ToListAsync();
            ViewBag.LoanProducts = loanProducts;
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
        public IActionResult KYCUpload()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KYCUpload(KycUploadViewModel model)
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

            if (ModelState.IsValid)
            {
                string contentRootPath = Directory.GetCurrentDirectory();
                string uploadFolder = Path.Combine(contentRootPath, "kyc_documents");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                try
                {
                    string identityFileName = null;
                    if (model.IdentityProofFile != null)
                    {
                        string fileExtension = Path.GetExtension(model.IdentityProofFile.FileName);
                        identityFileName = $"{customerId}_{Guid.NewGuid()}_identity{fileExtension}";
                        string filePath = Path.Combine(uploadFolder, identityFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.IdentityProofFile.CopyToAsync(fileStream);
                        }
                    }

                    var kycApproval = new KycApproval
                    {
                        CustomerId = customerId,
                        SubmissionDate = DateTime.UtcNow,
                        Status = "Pending",
                        DocumentPath = identityFileName,
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
                                                .Where(l => l.LoanStatus != "Closed") // NEW: Filter out closed loans
                                                .ToListAsync();

            // Simulate loan disbursement for approved loans if they haven't been disbursed yet
            foreach (var loan in approvedLoans)
            {
                if (loan.LoanStatus == "Pending Disbursement")
                {
                    await SimulateLoanDisbursement(loan);
                }
            }
            await _context.SaveChangesAsync(); // Save changes after potential disbursements

            return View(approvedLoans);
        }

        // GET: Customer/MakePayment/5
        [HttpGet]
        public async Task<IActionResult> MakePayment(int loanId)
        {
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                TempData["ErrorMessage"] = "Authentication error: Customer ID not found.";
                return RedirectToAction("Login", "Account"); // Assuming your login action is in Account controller
            }

            var loan = await _context.LoanApplications
                                     .Include(l => l.LoanProduct) // Include LoanProduct for product name
                                     .FirstOrDefaultAsync(l => l.ApplicationId == loanId && l.CustomerId == currentCustomerId);

            if (loan == null)
            {
                TempData["ErrorMessage"] = "Loan not found or you do not have permission to access it.";
                return RedirectToAction("AcceptedLoans"); // Redirect to a relevant loan list page
            }

            // --- Core Logic: Calculate Overdue Status and Amount Due ---
            DateTime today = DateTime.Today;
            bool loanStateChanged = false;

            // Handle closed loans first
            if (loan.LoanStatus == "Closed" && loan.OutstandingBalance <= 0)
            {
                loan.AmountDue = 0;
                loan.OverdueMonths = 0;
                loan.CurrentOverdueAmount = 0;
                loan.NextDueDate = null;
                loanStateChanged = true;
            }
            else // Loan is active or potentially overdue
            {
                // Ensure NextDueDate is set if it's null (e.g., after initial disbursement)
                if (!loan.NextDueDate.HasValue)
                {
                    // Default to 1st of next month, or current month if today is past 1st
                    loan.NextDueDate = new DateTime(today.Year, today.Month, 1).AddMonths(1);
                    loanStateChanged = true;
                }

                int calculatedOverdueMonths = 0;
                decimal calculatedOverdueAmount = 0;

                // Only calculate overdue if the loan is active/overdue and not fully paid
                if ((loan.LoanStatus == "Active" || loan.LoanStatus == "Overdue") && loan.OutstandingBalance > 0)
                {
                    DateTime tempDueDate = loan.NextDueDate.Value.Date;

                    // Loop to count full months that have passed since the last due date
                    // and accumulate overdue amounts, capping at outstanding balance.
                    while (tempDueDate < today) // Only count full overdue months
                    {
                        decimal potentialAccrual = loan.EMI;
                        decimal remainingBalanceAfterCurrentAccruals = loan.OutstandingBalance - calculatedOverdueAmount;

                        if (potentialAccrual > remainingBalanceAfterCurrentAccruals)
                        {
                            potentialAccrual = remainingBalanceAfterCurrentAccruals;
                        }

                        if (potentialAccrual > 0)
                        {
                            calculatedOverdueAmount += potentialAccrual;
                            calculatedOverdueMonths++;
                        }
                        else
                        {
                            // If no more outstanding balance to accrue, stop.
                            break;
                        }
                        tempDueDate = tempDueDate.AddMonths(1); // Move to the next potential due date
                    }
                }

                // Update loan object with calculated overdue values if they changed
                if (loan.OverdueMonths != calculatedOverdueMonths || loan.CurrentOverdueAmount != calculatedOverdueAmount)
                {
                    loan.OverdueMonths = calculatedOverdueMonths;
                    loan.CurrentOverdueAmount = calculatedOverdueAmount;
                    loan.LoanStatus = (calculatedOverdueMonths > 0) ? "Overdue" : "Active";
                    loanStateChanged = true;
                }
                else if (loan.OverdueMonths == 0 && loan.LoanStatus == "Overdue")
                {
                    // If it was overdue but now isn't (e.g., due to a recent payment), reset status
                    loan.LoanStatus = "Active";
                    loanStateChanged = true;
                }

                // Calculate the total AmountDue for the current payment cycle
                // This is the current EMI + any accumulated overdue amount, capped by outstanding balance.
                decimal totalAmountExpected = loan.EMI + loan.CurrentOverdueAmount;

                if (loan.OutstandingBalance <= 0)
                {
                    loan.AmountDue = 0;
                }
                else if (loan.OutstandingBalance < totalAmountExpected)
                {
                    loan.AmountDue = loan.OutstandingBalance; // Amount due cannot exceed outstanding balance
                }
                else
                {
                    loan.AmountDue = totalAmountExpected;
                }
            }

            if (loanStateChanged)
            {
                await _context.SaveChangesAsync();
            }

            // Prepare ViewModel
            var model = new MakePaymentViewModel
            {
                LoanId = loan.ApplicationId,
                ProductName = loan.LoanProduct?.ProductName,
                ShortDescription = $"Your monthly installment for loan number {loan.LoanNumber}.",
                MonthlyInstallmentAmount = loan.EMI,
                AmountDue = loan.AmountDue, // This now includes overdue amounts and is capped
                DueDate = loan.NextDueDate,
                OutstandingBalance = loan.OutstandingBalance,
                MinPayment = loan.EMI, // EMI is typically the minimum payment
                IsPaymentDue = loan.NextDueDate.HasValue && loan.NextDueDate.Value.Date <= today && loan.AmountDue > 0, // Payment is due if date is past/today AND there's an amount due
                OverdueMonths = loan.OverdueMonths,
                CurrentOverdueAmount = loan.CurrentOverdueAmount,
                IsOverdue = loan.CurrentOverdueAmount > 0, // IsOverdue is true only if CurrentOverdueAmount > 0
                LoanStatus = loan.LoanStatus // Pass loan status to view
            };

            return View("makepayment", model);
        }

        // POST: /Loan/ProcessPayment (This will be called via AJAX)
        [HttpPost]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int currentCustomerId))
            {
                return Unauthorized(new { success = false, message = "Authentication error: Customer ID not found." });
            }

            var loan = await _context.LoanApplications
                                     .FirstOrDefaultAsync(l => l.ApplicationId == request.LoanId && l.CustomerId == currentCustomerId);

            if (loan == null)
            {
                return NotFound(new { success = false, message = "Loan not found or does not belong to the customer." });
            }

            if (request.PaymentAmount <= 0)
            {
                return BadRequest(new { success = false, message = "Payment amount must be positive." });
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
                var newPayment = new LoanPayment
                {
                    LoanId = loan.ApplicationId,
                    CustomerId = currentCustomerId,
                    PaidAmount = request.PaymentAmount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = "TRX" + Guid.NewGuid().ToString().Substring(0, 8),
                    Status = "Success"
                };
                _context.LoanPayments.Add(newPayment);

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        decimal paymentToApply = request.PaymentAmount;
                        bool overdueClearedInThisPayment = false;
                        bool currentEMICoveredInThisPayment = false;

                        // 1. Apply payment to CurrentOverdueAmount first
                        if (loan.CurrentOverdueAmount > 0)
                        {
                            if (paymentToApply >= loan.CurrentOverdueAmount)
                            {
                                paymentToApply -= loan.CurrentOverdueAmount;
                                loan.CurrentOverdueAmount = 0;
                                loan.OverdueMonths = 0; // Clear overdue status
                                loan.LoanStatus = "Active"; // Reset status to active
                                overdueClearedInThisPayment = true;
                            }
                            else
                            {
                                loan.CurrentOverdueAmount -= paymentToApply;
                                paymentToApply = 0; // Payment fully used for overdue
                                overdueClearedInThisPayment = false; // Overdue not fully cleared
                            }
                        }

                        // 2. Apply remaining payment towards the current EMI
                        // This applies if there's payment left and the loan is now active or was just cleared from overdue.
                        if (paymentToApply > 0 && (loan.LoanStatus == "Active" || overdueClearedInThisPayment))
                        {
                            decimal amountToCoverForCurrentEMI = loan.EMI;

                            if (paymentToApply >= amountToCoverForCurrentEMI)
                            {
                                paymentToApply -= amountToCoverForCurrentEMI;
                                currentEMICoveredInThisPayment = true;
                            }
                            else
                            {
                                // Partial payment for the current EMI
                                currentEMICoveredInThisPayment = false;
                            }
                        }

                        // 3. Update OutstandingBalance directly with the total payment received
                        loan.OutstandingBalance -= request.PaymentAmount;

                        // 4. Determine new NextDueDate and AmountDue based on payment impact
                        if (loan.OutstandingBalance <= 0)
                        {
                            // Loan fully paid
                            loan.OutstandingBalance = 0;
                            loan.LoanStatus = "Closed";
                            loan.EMI = 0;
                            loan.AmountDue = 0;
                            loan.NextDueDate = null;
                            loan.OverdueMonths = 0;
                            loan.CurrentOverdueAmount = 0;
                        }
                        else
                        {
                            // If overdue was cleared AND current EMI was covered, advance NextDueDate.
                            if (overdueClearedInThisPayment && currentEMICoveredInThisPayment)
                            {
                                // Advance to the 1st of the next month from TODAY
                                loan.NextDueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
                                loan.AmountDue = loan.EMI; // Set AmountDue for the newly advanced month
                            }
                            else if (loan.CurrentOverdueAmount == 0 && loan.OutstandingBalance > 0 && loan.NextDueDate.HasValue && loan.NextDueDate.Value.Date < DateTime.Today)
                            {
                                // This handles the case where overdue was cleared, but the current month's EMI wasn't fully paid.
                                // Or it's a new month and EMI is due.
                                // If NextDueDate is in the past, but no longer overdue (meaning the payment took care of previous EMIs)
                                // Then, advance NextDueDate to the 1st of the current month (if it's not already) or next month.
                                // This ensures NextDueDate is always relevant and moves forward.
                                if (loan.NextDueDate.Value.Month != DateTime.Today.Month || loan.NextDueDate.Value.Year != DateTime.Today.Year)
                                {
                                    loan.NextDueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                                }
                                // If today is past the 1st of the current month and the due date is still for this month's start or earlier,
                                // we should consider it due for this month.
                                if (loan.NextDueDate.Value.Date < DateTime.Today)
                                {
                                    loan.NextDueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
                                }

                                loan.AmountDue = loan.EMI; // The EMI for the next cycle
                            }
                            else
                            {
                                // If overdue was not fully cleared, or current EMI was only partially covered,
                                // NextDueDate should not advance. AmountDue will be re-calculated on the next GET.
                                // But let's ensure AmountDue is updated to reflect any remaining for the current cycle.
                                decimal calculatedAmountDueForNextDisplay = loan.EMI + loan.CurrentOverdueAmount;
                                if (calculatedAmountDueForNextDisplay > loan.OutstandingBalance)
                                {
                                    calculatedAmountDueForNextDisplay = loan.OutstandingBalance;
                                }
                                loan.AmountDue = calculatedAmountDueForNextDisplay;
                            }
                        }

                        loan.LastPaymentDate = DateTime.Now; // Update last payment date

                        await _context.SaveChangesAsync();
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error processing payment: {ex.Message}");
                        return StatusCode(500, new { success = false, message = "An error occurred while updating loan details.", reason = ex.Message });
                    }
                }

                // Return updated values for frontend to refresh UI
                return Ok(new { success = true, message = "Payment successful!", transactionId = newPayment.TransactionId, newOutstandingBalance = loan.OutstandingBalance, newAmountDue = loan.AmountDue, newNextDueDate = loan.NextDueDate?.ToShortDateString(), newOverdueMonths = loan.OverdueMonths, newCurrentOverdueAmount = loan.CurrentOverdueAmount, newLoanStatus = loan.LoanStatus });
            }
            else
            {
                var failedPayment = new LoanPayment
                {
                    LoanId = loan.ApplicationId,
                    CustomerId = currentCustomerId,
                    PaidAmount = request.PaymentAmount,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = "FAILTRX" + Guid.NewGuid().ToString().Substring(0, 8),
                    Status = "Failed"
                };
                _context.LoanPayments.Add(failedPayment);
                await _context.SaveChangesAsync();

                return StatusCode(500, new { success = false, message = "Payment failed at gateway. Please try again.", reason = "Simulated insufficient funds." });
            }
        }
        // Helper method to simulate a payment gateway response
        private bool SimulatePaymentGateway(decimal amount, string method)
        {
            System.Threading.Thread.Sleep(500); // Simulate a short delay
            return new Random().NextDouble() < 0.85; // 85% success rate
        }

        /// <summary>
        /// Calculates the Equated Monthly Installment (EMI) for a loan.
        /// Formula: EMI = P * r * (1 + r)^n / ((1 + r)^n - 1)
        /// Where:
        /// P = Principal Loan Amount
        /// r = Monthly Interest Rate (Annual Rate / 12 / 100)
        /// n = Loan Tenure in Months
        /// </summary>
        /// <param name="principal">The principal loan amount.</param>
        /// <param name="annualInterestRate">The annual interest rate (e.g., 8.5 for 8.5%).</param>
        /// <param name="tenureMonths">The loan tenure in months.</param>
        /// <returns>The calculated EMI.</returns>
        private decimal CalculateEMI(decimal principal, decimal annualInterestRate, int tenureMonths)
        {
            if (tenureMonths <= 0) return principal; // Handle zero or negative tenure
            if (annualInterestRate <= 0) return principal / tenureMonths; // Simple division if no interest

            decimal monthlyInterestRate = (annualInterestRate / 100) / 12;

            double powerFactor = Math.Pow((double)(1 + monthlyInterestRate), tenureMonths);
            decimal emi = principal * monthlyInterestRate * (decimal)powerFactor / ((decimal)powerFactor - 1);

            return Math.Round(emi, 2); // Round to 2 decimal places for currency
        }

        /// <summary>
        /// Simulates the disbursement of an approved loan, setting initial financial details.
        /// In a real application, this would be triggered by an admin action.
        /// </summary>
        /// <param name="loan">The LoanApplication object to disburse.</param>
        private async Task SimulateLoanDisbursement(LoanApplication loan)
        {
            if (loan.ApprovalStatus == "Approved" && loan.LoanStatus == "Pending Disbursement")
            {
                loan.EMI = CalculateEMI(loan.LoanAmount, loan.InterestRate, loan.TenureMonths);
                loan.OutstandingBalance = loan.LoanAmount;
                // Set first due date to the 1st of the next month
                loan.NextDueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
                loan.AmountDue = loan.EMI; // Initial amount due is the first EMI
                loan.LoanStatus = "Active";
                loan.ApprovalDate = DateTime.Now; // Set approval date upon disbursement simulation
                loan.LastPaymentDate = null; // No payments made yet
                loan.OverdueMonths = 0;
                loan.CurrentOverdueAmount = 0;

                _context.LoanApplications.Update(loan);
                // No SaveChangesAsync here, it will be called by the calling action (e.g., AcceptedLoans)
            }
        }
    }
}