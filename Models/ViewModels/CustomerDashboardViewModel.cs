using System;
using System.Collections.Generic;

namespace CredWise_Trail.Models.ViewModels
{
    /// <summary>
    /// REVISED VIEWMODEL: This model now holds aggregated data for ALL active loans.
    /// </summary>
    public class CustomerDashboardViewModel
    {
        // Flag to check if the customer has any active loans at all.
        public bool HasActiveLoans { get; set; }

        // The number of active loans the customer has (e.g., "Summary of 3 Active Loans").
        public int ActiveLoanCount { get; set; }

        // --- AGGREGATED PROPERTIES FOR THE SUMMARY CARD ---

        // The SUM of the original principal amounts from all active loans.
        public decimal TotalPrincipalAmount { get; set; }

        // The SUM of the remaining balances from all active loans.
        public decimal TotalOutstandingBalance { get; set; }

        // The SUM of the next EMI amounts due for all active loans.
        public decimal TotalNextPaymentAmount { get; set; }

        // The EARLIEST due date among all active loans.
        public DateTime? EarliestNextDueDate { get; set; }

        // An overall progress percentage based on all loans combined.
        public int OverallProgressPercentage { get; set; }

        // --- Properties for the 'Recent Payment History' table (this part is unchanged) ---
        public List<RecentPaymentItem> RecentPayments { get; set; }

        public CustomerDashboardViewModel()
        {
            RecentPayments = new List<RecentPaymentItem>();
        }
    }

    /// <summary>
    /// Represents a single row in the 'Recent Payment History' table. (Unchanged)
    /// </summary>
    public class RecentPaymentItem
    {
        public DateTime? PaymentDate { get; set; }
        public string Description { get; set; } // e.g., "Payment for Home Loan (HL05682)"
        public string LoanNumber { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; }
    }
}