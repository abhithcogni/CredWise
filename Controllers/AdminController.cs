using CredWise_Trail.Models;
using CredWise_Trail.Models.ViewModels;
using CredWise_Trail.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims; // Add this using directive for CultureInfo

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

        
        public IActionResult CustomerDetails()
        {
            return View();
        }

        // API endpoint to get all customers with their loan application count
        [HttpGet]
        public async Task<IActionResult> GetAllCustomers()
        {
            var customers = await _context.Customers
                .Select(c => new CustomerViewModel
                {
                    CustomerId = c.CustomerId,
                    Name = c.Name,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    LoanApplicationCount = c.LoanApplications.Count()
                })
                .ToListAsync();

            return Json(customers);
        }

        // API endpoint to get detailed information for a single customer
        [HttpGet]
        public async Task<IActionResult> GetCustomerDetails(int customerId)
        {
            var customer = await _context.Customers
                .Where(c => c.CustomerId == customerId)
                .Include(c => c.LoanApplications)
                    .ThenInclude(la => la.LoanProduct)
                .Select(c => new CustomerDetailViewModel
                {
                    CustomerId = c.CustomerId,
                    Name = c.Name,
                    Email = c.Email,
                    PhoneNumber = c.PhoneNumber,
                    Address = c.Address,
                    AccountNumber = c.AccountNumber,
                    CreatedDate = c.CreatedDate,
                    LoanApplications = c.LoanApplications.Select(la => new LoanApplicationSummaryViewModel
                    {
                        ApplicationId = la.ApplicationId,
                        ProductName = la.LoanProduct.ProductName,
                        LoanAmount = la.LoanAmount,
                        ApplicationDate = la.ApplicationDate,
                        ApprovalStatus = la.ApprovalStatus
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (customer == null)
            {
                return NotFound(new { success = false, message = "Customer not found." });
            }

            return Json(customer);
        }
        public async Task<IActionResult> AdminDashboard()
        {
            var viewModel = new AdminDashboardViewModel();
            var currentYear = DateTime.Now.Year;

            // --- 1. Corrected Fetch Data for Summary Cards ---
            // This section now correctly calculates the key metrics for the dashboard's summary cards.

            // Total Loan Value: The principal amount of loans that are either active or overdue.
            // This represents the total capital that is currently deployed and outstanding.
            viewModel.TotalLoanValue = await _context.LoanApplications
                .Where(la => la.ApprovalStatus == LoanApprovalStatus.APPROVED.ToString() &&
                             (la.LoanStatus == LoanOverallStatus.ACTIVE.ToString() || la.LoanStatus == LoanOverallStatus.OVERDUE.ToString()))
                .SumAsync(la => la.LoanAmount);

            // Active Loans Count: The total number of loans that are currently in an 'ACTIVE' state.
            // These are loans that are being repaid on schedule.
            viewModel.ActiveLoansCount = await _context.LoanApplications
                .CountAsync(la => la.LoanStatus == LoanOverallStatus.ACTIVE.ToString());

            // Pending Applications Count: The number of loan applications that are awaiting a decision.
            // This uses the 'PENDING' status from the LoanApprovalStatus enum.
            viewModel.PendingApplicationsCount = await _context.LoanApplications
                .CountAsync(la => la.ApprovalStatus == LoanApprovalStatus.PENDING.ToString());

            // Overdue Loans Count: The total number of loans that are currently marked as 'OVERDUE'.
            // These are loans where payments have been missed.
            viewModel.OverdueLoansCount = await _context.LoanApplications
                .CountAsync(la => la.LoanStatus == LoanOverallStatus.OVERDUE.ToString());


            // --- 2. Fetch Data for Loan Performance Chart (Bar Chart) ---
            // This section fetches data to show new loans and repayments over the current year.

            // Retrieves the sum of approved loan amounts for each month of the current year.
            var newLoansByMonth = await _context.LoanApplications
                .Where(la => la.ApprovalStatus == LoanApprovalStatus.APPROVED.ToString() && la.ApplicationDate.Year == currentYear)
                .GroupBy(la => la.ApplicationDate.Month)
                .Select(g => new { Month = g.Key, TotalAmount = g.Sum(la => la.LoanAmount) })
                .ToListAsync();

            // Retrieves the sum of successful repayment amounts for each month of the current year.
            // Note: Assumes a 'LoanPayment' entity and 'Success' status string.
            var repaymentsByMonth = await _context.LoanPayments // Make sure you have a LoanPayments DbSet in your context
                .Where(lp => lp.PaymentDate.HasValue && lp.PaymentDate.Value.Year == currentYear && lp.Status == "Success")
                .GroupBy(lp => lp.PaymentDate.Value.Month)
                .Select(g => new { Month = g.Key, TotalAmount = g.Sum(lp => lp.PaidAmount) })
                .ToListAsync();

            // Initialize arrays to hold data for all 12 months.
            var newLoansData = new decimal[12];
            var repaymentsData = new decimal[12];
            var monthlyLabels = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray();

            // Populate the arrays with the fetched data, converting amounts to 'thousands' for the chart.
            foreach (var item in newLoansByMonth)
            {
                newLoansData[item.Month - 1] = item.TotalAmount / 1000;
            }

            foreach (var item in repaymentsByMonth)
            {
                repaymentsData[item.Month - 1] = item.TotalAmount / 1000;
            }

            // Assign the prepared data to the ViewModel.
            viewModel.MonthlyLabels = monthlyLabels.ToList();
            viewModel.NewLoansMonthlyData = newLoansData.ToList();
            viewModel.RepaymentsMonthlyData = repaymentsData.ToList();


            // --- 3. Corrected Fetch Data for Loan Status Distribution Chart (Doughnut Chart) ---
            // This logic populates the doughnut chart. The counts are directly taken from the
            // summary card calculations to ensure data consistency across the dashboard.

            // The labels for the doughnut chart segments.
            viewModel.LoanStatusLabels = new List<string> {
            "Active",
            "Pending Approval",
            "Overdue"
        };

            // The data for the doughnut chart segments, using the counts calculated in step 1.
            viewModel.LoanStatusCounts = new List<int> {
            viewModel.ActiveLoansCount,
            viewModel.PendingApplicationsCount,
            viewModel.OverdueLoansCount
        };


            // --- 4. Fetch Data for Recent Loan Applications Table ---
            // This section retrieves the 5 most recent loan applications to display in a table.

            viewModel.RecentLoanApplications = await _context.LoanApplications
                .Include(la => la.Customer) // Eagerly loads the related Customer entity.
                .Include(la => la.LoanProduct) // Eagerly loads the related LoanProduct entity.
                .OrderByDescending(la => la.ApplicationDate) // Orders applications by date, newest first.
                .Take(5) // Limits the result to the top 5.
                .ToListAsync();

            // Pass the fully populated ViewModel to the view for rendering.
            return View(viewModel);
        }

        public async Task<IActionResult> KycApproval(string status, int? pageNumber)
        {
            // Pass the current filter to the view so we can keep it when changing pages
            ViewData["CurrentFilter"] = status;

            // 1. Declare the model variable that we will return at the end.
            PaginatedList<KycApproval> paginatedModel;

            try
            {
                int pageSize = 5;
                IQueryable<KycApproval> source = _context.KycApprovals
                                                        .Include(k => k.Customer)
                                                        .OrderBy(k => k.KycID);

                // If a status filter is provided, add a WHERE clause to the query
                if (!String.IsNullOrEmpty(status) && status != "All")
                {
                    source = source.Where(k => k.Status == status);
                }

                // 2. Populate the model inside the 'try' block on success.
                paginatedModel = await PaginatedList<KycApproval>.CreateAsync(source, pageNumber ?? 1, pageSize);
            }
            catch (Exception ex)
            {
                // Log the error as before
                _logger.LogError(ex, "Error fetching KYC approvals for display.");

                // 3. Populate the model with an empty list inside the 'catch' block on failure.
                // This ensures the view still receives a valid model and doesn't crash.
                var emptySource = new List<KycApproval>().AsQueryable();
                paginatedModel = await PaginatedList<KycApproval>.CreateAsync(emptySource, 1, 10);

                // Optionally, add a model error to display a friendly message to the user on the view.
                ModelState.AddModelError(string.Empty, "An error occurred while retrieving KYC applications. Please try again later.");
            }

            // 4. Return the model. This return statement is now guaranteed to be hit on all code paths.
            return View(paginatedModel);
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

        public async Task<IActionResult> LoanApproval(string status = "All", int page = 1)
        {
            // --- Step 1: Define Pagination Parameters ---
            // We set a fixed number of items to display per page.
            const int pageSize = 5;

            // --- Step 2: Create the Base Query ---
            // We start with a base query that includes the related Customer and LoanProduct.
            // Using AsQueryable() allows us to build upon this query step-by-step before it's sent to the database.
            var query = _context.LoanApplications
                                .Include(la => la.Customer)
                                .Include(la => la.LoanProduct)
                                .AsQueryable();

            // --- Step 3: Apply Server-Side Filtering ---
            // If a specific status (other than "All") is provided in the URL (e.g., /Admin/LoanApproval?status=Pending),
            // we add a WHERE clause to our query to filter the results.
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(la => la.ApprovalStatus == status);
            }

            // --- Step 4: Get the Total Count for Pagination ---
            // We need to know the total number of items that match our filter to calculate the total number of pages.
            // This count is performed on the filtered query before we take just one page.
            var totalItems = await query.CountAsync();

            // --- Step 5: Fetch the Paginated Data ---
            // This is the core of pagination.
            // We first order the applications by date.
            // .Skip() bypasses a number of records based on the current page and page size.
            // .Take() then selects the next 'pageSize' number of records.
            // Finally, ToListAsync() executes the query against the database.
            var loanApplications = await query
                                        .OrderByDescending(la => la.ApplicationDate)
                                        .Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();

            // --- Step 6: Store Pagination and Filter Data for the View ---
            // We pass key information to the view using ViewData so we can build the pagination controls
            // and correctly display the current state.
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewData["CurrentFilter"] = status; // To remember the selected filter on the dropdown.

            // --- Step 7: Return the View with the Paginated List ---
            // We pass the list of applications for the current page to the view.
            return View(loanApplications);
        }

        // POST: Admin/UpdateLoanStatus (for AJAX calls to update status)
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> UpdateLoanStatus(int loanId, string status)
        {
            var loanApplication = await _context.LoanApplications.FindAsync(loanId);

            if (loanApplication == null)
            {
                return NotFound(new { success = false, message = "Loan application not found." });
            }

            // Validate status input
            var validStatuses = new List<string> { "Approved", "Rejected", "Pending" };
            if (!validStatuses.Contains(status))
            {
                return BadRequest(new { success = false, message = "Invalid status provided." });
            }

            loanApplication.ApprovalStatus = status;

            if (status == "Approved") // Based on 'APPROVED' ENUM value
            {
                loanApplication.ApprovalDate = DateTime.Now;
                loanApplication.LoanStatus = "Active"; // Loan becomes active upon approval

                // Ensure LoanAmount, InterestRate (annual %), TenureMonths, EMI (regular amount) are pre-set.
                decimal principal = loanApplication.LoanAmount;
                decimal annualInterestRate = loanApplication.InterestRate; // e.g., 10 for 10%
                int tenureInMonths = loanApplication.TenureMonths;
                decimal regularEmiAmount = loanApplication.EMI; // Fixed regular EMI

                // Set initial financial details
                loanApplication.OutstandingBalance = principal;
                var approvalDate = loanApplication.ApprovalDate ?? DateTime.Now; // ApprovalDate should be set here
                loanApplication.NextDueDate = new DateTime(approvalDate.Year, approvalDate.Month, approvalDate.Day).AddMonths(1);
                // loanApplication.AmountDue is set after schedule generation from the first installment.

                // --- Generate Repayment Schedule ---
                // Clear existing schedule if any (e.g., for re-approval)
                var existingRepayments = _context.Repayments.Where(r => r.ApplicationId == loanApplication.ApplicationId);
                _context.Repayments.RemoveRange(existingRepayments);

                List<Repayment> repayments = new List<Repayment>();
                decimal currentBalanceForSchedule = principal;
                decimal monthlyInterestRateDecimal = annualInterestRate / 12 / 100;

                for (int i = 1; i <= tenureInMonths; i++)
                {
                    DateTime dueDate = (loanApplication.NextDueDate ?? DateTime.Now.Date).AddMonths(i - 1);

                    decimal interestComponent = Math.Round(currentBalanceForSchedule * monthlyInterestRateDecimal, 2);
                    decimal principalComponent;
                    decimal actualEmiForThisMonth;

                    if (i < tenureInMonths) // For all regular EMIs
                    {
                        actualEmiForThisMonth = regularEmiAmount;
                        principalComponent = actualEmiForThisMonth - interestComponent;
                        if (principalComponent < 0) principalComponent = 0; // Prevent negative principal component
                                                                            // Adjust if pre-defined EMI is too high for remaining balance before the second to last EMI
                        if (currentBalanceForSchedule - principalComponent < 0 && (i < tenureInMonths - 1))
                        {
                            principalComponent = currentBalanceForSchedule;
                            actualEmiForThisMonth = principalComponent + interestComponent;
                        }
                    }
                    else // For the last EMI
                    {
                        // Last EMI clears the remaining balance plus its interest
                        principalComponent = currentBalanceForSchedule;
                        actualEmiForThisMonth = principalComponent + interestComponent;
                    }
                    actualEmiForThisMonth = Math.Round(actualEmiForThisMonth, 2);

                    repayments.Add(new Repayment
                    {
                        ApplicationId = loanApplication.ApplicationId,
                        DueDate = dueDate,
                        AmountDue = actualEmiForThisMonth,
                        PaymentStatus = "PENDING", // Corresponds to 'PENDING' ENUM
                        PaymentDate = null
                    });

                    currentBalanceForSchedule -= principalComponent;
                    if (currentBalanceForSchedule < 0) currentBalanceForSchedule = 0; // Prevent negative balance
                }

                // Set LoanApplication.AmountDue to the first installment's amount
                if (repayments.Any())
                {
                    loanApplication.AmountDue = repayments.First().AmountDue;
                }
                else if (tenureInMonths == 0) // Edge case: 0 tenure
                {
                    loanApplication.AmountDue = principal;
                }

                await _context.Repayments.AddRangeAsync(repayments);
                // --- End of Repayment Schedule Generation ---

                // Set a unique LoanNumber if not already set
                if (string.IsNullOrEmpty(loanApplication.LoanNumber))
                {
                    loanApplication.LoanNumber = $"LN{DateTime.Now:yyyyMMdd}-{loanApplication.ApplicationId % 1000:D3}";
                }
            }
            else if (status == "Rejected") // Based on 'REJECTED' ENUM value
            {
                loanApplication.ApprovalDate = DateTime.Now;
                loanApplication.LoanStatus = "Closed";
                loanApplication.EMI = 0;
                loanApplication.AmountDue = 0;
                loanApplication.OutstandingBalance = 0;
                loanApplication.NextDueDate = null;
                loanApplication.TenureMonths = 0;
                loanApplication.InterestRate = 0; // Clear any tentatively set interest
            }
            else // "Pending"
            {
                loanApplication.ApprovalDate = null;
                loanApplication.LoanStatus = "Pending";
                // Clear financial details if loan is reverted to pending
                loanApplication.EMI = 0;
                loanApplication.AmountDue = 0;
                loanApplication.OutstandingBalance = 0;
                loanApplication.NextDueDate = null;
                // TenureMonths and InterestRate are typically part of application, so not reset here.
            }

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, newStatus = loanApplication.ApprovalStatus, loanId = loanApplication.ApplicationId, message = $"Loan status updated to {status} and repayment schedule generated if approved." });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Log ex for diagnostics
                Console.WriteLine($"Concurrency conflict for LoanId {loanId}: {ex.Message}");
                return StatusCode(409, new { success = false, message = "Concurrency conflict: Loan was already updated. Please refresh." });
            }
            catch (Exception ex)
            {
                // Log ex for diagnostics
                Console.WriteLine($"Error updating loan status for LoanId {loanId}: {ex.Message} {ex.StackTrace}");
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