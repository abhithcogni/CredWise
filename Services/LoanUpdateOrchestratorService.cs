using CredWise_Trail.Models; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CredWise_Trail.Services
{
    public class LoanUpdateOrchestratorService
    {
        private readonly ILogger<LoanUpdateOrchestratorService> _logger;
        private readonly BankLoanManagementDbContext _context;

        public LoanUpdateOrchestratorService(ILogger<LoanUpdateOrchestratorService> logger, BankLoanManagementDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public async Task TriggerLoanStatusUpdateAsync()
        {
            _logger.LogInformation("On-demand loan status update process triggered.");

            DateTime today = DateTime.Now.Date;

            var potentiallyOverdueLoans = await _context.LoanApplications
                .Include(la => la.Repayments) 
                .Where(la => la.LoanStatus == "Active" &&
                             la.NextDueDate.HasValue &&
                             la.NextDueDate.Value.Date < today)
                .ToListAsync();

            if (!potentiallyOverdueLoans.Any())
            {
                _logger.LogInformation("No loans required a status update during this run.");
                return;
            }

            _logger.LogInformation("Found {count} loans that may need to be marked as overdue.", potentiallyOverdueLoans.Count);

            foreach (var loan in potentiallyOverdueLoans)
            {
                var missedRepayment = loan.Repayments
                                          .FirstOrDefault(r => r.DueDate.Date == loan.NextDueDate.Value.Date);

                if (missedRepayment != null && missedRepayment.PaymentStatus == "PENDING")
                {
                    _logger.LogWarning("Updating Loan ID {loanId} to OVERDUE status. Due date {dueDate} was missed.", loan.ApplicationId, loan.NextDueDate.Value.ToShortDateString());
                    loan.LoanStatus = "Overdue";

                    var allPastDueRepayments = loan.Repayments
                        .Where(r => r.PaymentStatus == "PENDING" && r.DueDate.Date < today)
                        .ToList();

                    loan.OverdueMonths = allPastDueRepayments.Count;
                    loan.CurrentOverdueAmount = allPastDueRepayments.Sum(r => r.AmountDue);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully completed the on-demand loan status update process.");
        }
    }
}