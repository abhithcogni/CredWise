using System;
using System.Collections.Generic;
using System.Linq; // Added for LINQ operations like .Sum() and .Count()

namespace CredWise_Trail.Models.ViewModels
{
    public class CustomerStatementViewModel
    {
        public LoanOverviewSummary LoanOverview { get; set; }

        public IEnumerable<LoanAccountListItem> LoanAccounts { get; set; }

        /// <summary>
        /// Gets or sets a list of detailed statements for each loan account.
        /// The view will iterate through this list to find and display the selected loan's details.
        /// </summary>
        public IEnumerable<LoanStatementDetail> LoanStatements { get; set; }
    }

    /// <summary>
    /// Represents the summary data for the customer's loan portfolio displayed in the "Loan Overview" section.
    /// </summary>
    public class LoanOverviewSummary
    {
        /// <summary>
        /// Gets or sets the total number of active loans for the customer.
        /// This is derived from the number of loans with an "Active" or "Pending Disbursement" status.
        /// </summary>
        public int TotalActiveLoans { get; set; }

        /// <summary>
        /// Gets or sets the total amount disbursed across all active loans.
        /// This sums up the original 'LoanAmount' for all relevant loans.
        /// </summary>
        public decimal TotalAmountDisbursed { get; set; }

        /// <summary>
        /// Gets or sets the sum of outstanding balances across all active loans.
        /// </summary>
        public decimal TotalOutstanding { get; set; }
    }

    /// <summary>
    /// Represents a simplified view of a loan account, primarily for populating the dropdown list.
    /// </summary>
    public class LoanAccountListItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the loan application (e.g., "LNAPP001").
        /// This will be the `value` attribute of the dropdown option.
        /// </summary>
        public string LoanApplicationNumber { get; set; }

        /// <summary>
        /// Gets or sets the display text for the loan in the dropdown (e.g., "Home Loan (LNAPP001) - ₹50,000").
        /// </summary>
        public string DisplayText { get; set; }
    }

    /// <summary>
    /// Represents the detailed statement for a single loan account, including its metadata and repayment history.
    /// </summary>
    public class LoanStatementDetail
    {
        /// <summary>
        /// Gets or sets the unique identifier for the loan application (e.g., "LNAPP001").
        /// This will be used to match against the selected dropdown value.
        /// </summary>
        public string LoanApplicationNumber { get; set; }

        /// <summary>
        /// Gets or sets the actual ID of the loan application from the database.
        /// </summary>
        public int ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the name of the loan product (e.g., "Home Loan", "Personal Loan").
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Gets or sets the original sanctioned loan amount.
        /// </summary>
        public decimal LoanAmount { get; set; }

        /// <summary>
        /// Gets or sets the interest rate applicable to this loan.
        /// </summary>
        public decimal InterestRate { get; set; }

        /// <summary>
        /// Gets or sets the tenure of the loan in months.
        /// </summary>
        public int TenureMonths { get; set; }

        /// <summary>
        /// Gets or sets the date the loan application was submitted.
        /// </summary>
        public DateTime ApplicationDate { get; set; }

        /// <summary>
        /// Gets or sets the approval status of the loan (e.g., "APPROVED").
        /// </summary>
        public string ApprovalStatus { get; set; }

        /// <summary>
        /// Gets or sets the current outstanding principal and accrued interest balance.
        /// </summary>
        public decimal OutstandingBalance { get; set; }

        /// <summary>
        /// Gets or sets the collection of individual repayment transactions for this loan.
        /// </summary>
        public IEnumerable<RepaymentRecord> RepaymentHistory { get; set; }
    }

    /// <summary>
    /// Represents a single repayment record within the loan statement.
    /// </summary>
    public class RepaymentRecord
    {
        /// <summary>
        /// Gets or sets the unique ID for the repayment transaction.
        /// </summary>
        public int PaymentId { get; set; }

        /// <summary>
        /// Gets or sets the date the payment was due. This is derived from the loan's repayment schedule.
        /// </summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// Gets or sets the amount that was due for this installment.
        /// In a real system, this would likely be part of a schedule, or derived based on EMI and outstanding.
        /// For simplicity, we might assume it's the EMI or a specific payment request amount.
        /// </summary>
        public decimal AmountDue { get; set; }

        /// <summary>
        /// Gets or sets the actual date the payment was made. Nullable if payment is pending.
        /// </summary>
        public DateTime? PaymentDate { get; set; }

        /// <summary>
        /// Gets or sets the status of the repayment (e.g., "COMPLETED", "PENDING", "OVERDUE").
        /// </summary>
        public string Status { get; set; }  
    }
}