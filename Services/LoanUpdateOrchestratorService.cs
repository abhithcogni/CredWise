// In folder: Services/LoanUpdateOrchestratorService.cs

using CredWise_Trail.Models; // Your DbContext namespace
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CredWise_Trail.Services
{
    /// <summary>
    /// This service orchestrates the process of updating loan statuses.
    /// Its logic is designed to be called on-demand from different parts of the application,
    /// such as from a controller after a user logs in, or from a scheduled background job.
    /// This decouples the core business logic from the trigger mechanism.
    /// </summary>
    public class LoanUpdateOrchestratorService
    {
        private readonly ILogger<LoanUpdateOrchestratorService> _logger;
        private readonly BankLoanManagementDbContext _context;

        /// <summary>
        /// Constructor for the orchestrator service.
        /// </summary>
        /// <param name="logger">Used for logging information and errors.</param>
        /// <param name="context">
        /// The database context, injected by the dependency injection container.
        /// Since this service will be 'Scoped' or 'Transient', we can safely inject the DbContext directly,
        //  as it will be resolved correctly for each web request.
        /// </param>
        public LoanUpdateOrchestratorService(ILogger<LoanUpdateOrchestratorService> logger, BankLoanManagementDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// This is the main public method that triggers the entire loan status update process.
        /// It finds and updates all loans that have become overdue.
        /// </summary>
        public async Task TriggerLoanStatusUpdateAsync()
        {
            _logger.LogInformation("On-demand loan status update process triggered.");

            // Get today's date, ignoring the time component for accurate comparisons.
            DateTime today = DateTime.Now.Date;

            // --- Core Logic: Find all loans that are 'Active' but have a missed due date ---
            // This is the heart of the batch process. It identifies loans that require attention.
            var potentiallyOverdueLoans = await _context.LoanApplications
                .Include(la => la.Repayments) // Eagerly load the repayment schedules to avoid multiple database calls.
                .Where(la => la.LoanStatus == "Active" &&
                             la.NextDueDate.HasValue &&
                             la.NextDueDate.Value.Date < today)
                .ToListAsync();

            if (!potentiallyOverdueLoans.Any())
            {
                _logger.LogInformation("No loans required a status update during this run.");
                return; // Exit early if there's nothing to do.
            }

            _logger.LogInformation("Found {count} loans that may need to be marked as overdue.", potentiallyOverdueLoans.Count);

            foreach (var loan in potentiallyOverdueLoans)
            {
                // We must verify that the missed installment is still marked as 'PENDING'.
                // It's possible the user paid on the due date and the system just hasn't processed it yet.
                var missedRepayment = loan.Repayments
                                          .FirstOrDefault(r => r.DueDate.Date == loan.NextDueDate.Value.Date);

                if (missedRepayment != null && missedRepayment.PaymentStatus == "PENDING")
                {
                    _logger.LogWarning("Updating Loan ID {loanId} to OVERDUE status. Due date {dueDate} was missed.", loan.ApplicationId, loan.NextDueDate.Value.ToShortDateString());

                    // --- Apply the state change to the LoanApplication entity ---

                    // 1. Change the primary status to 'OVERDUE'.
                    loan.LoanStatus = "Overdue";

                    // 2. Recalculate the specific overdue metrics based on the full repayment schedule.
                    // This logic is centralized here to be consistent across the application.
                    var allPastDueRepayments = loan.Repayments
                        .Where(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today)
                        .ToList();

                    loan.OverdueMonths = allPastDueRepayments.Count;
                    loan.CurrentOverdueAmount = allPastDueRepayments.Sum(r => r.AmountDue);
                }
            }

            // After iterating through all the loans and marking the necessary ones as overdue,
            // we save all these changes to the database in a single, efficient transaction.
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully completed the on-demand loan status update process.");
        }
    }
}