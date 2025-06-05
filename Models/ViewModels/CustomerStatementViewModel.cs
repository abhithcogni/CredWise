// --- In ViewModels/CustomerViewModels.cs (or your equivalent file) ---
using System;
using System.Collections.Generic;

namespace CredWise_Trail.Models.ViewModels
{
    public class CustomerStatementViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }

        // Overall Summary
        public int TotalActiveLoans { get; set; }
        public decimal TotalAmountDisbursed { get; set; }
        public decimal TotalOutstandingAmount { get; set; }

        // For dropdown
        public List<LoanAccountSelectItemViewModel> LoanAccountsForSelection { get; set; }
        // Detailed statements
        public List<LoanStatementDetailViewModel> LoanStatements { get; set; }

        // NEW: Property to hold any error messages for the view
        public string ErrorMessage { get; set; }

        public CustomerStatementViewModel()
        {
            LoanAccountsForSelection = new List<LoanAccountSelectItemViewModel>();
            LoanStatements = new List<LoanStatementDetailViewModel>();
            // ErrorMessage will be null by default
        }
    }

    // LoanAccountSelectItemViewModel, LoanStatementDetailViewModel,
    // and RepaymentHistoryItemViewModel remain the same as previously defined.
    // (Make sure they are in this file or accessible)

    public class LoanAccountSelectItemViewModel
    {
        public string LoanIdValue { get; set; }
        public string LoanDisplayText { get; set; }
    }

    public class LoanStatementDetailViewModel
    {
        public string UniqueLoanIdentifier { get; set; }
        public string ApplicationIdDisplay { get; set; }
        public string ProductName { get; set; }
        public decimal LoanAmount { get; set; }
        public decimal InterestRate { get; set; }
        public int TenureMonths { get; set; }
        public DateTime ApplicationDate { get; set; }
        public string ApprovalStatus { get; set; }
        public decimal OutstandingBalance { get; set; }
        public List<RepaymentHistoryItemViewModel> RepaymentHistory { get; set; }

        public LoanStatementDetailViewModel()
        {
            RepaymentHistory = new List<RepaymentHistoryItemViewModel>();
        }
    }

    public class RepaymentHistoryItemViewModel
    {
        public int RepaymentId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal AmountDue { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentStatus { get; set; }
    }
}