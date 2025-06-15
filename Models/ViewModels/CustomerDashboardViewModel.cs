using System;
using System.Collections.Generic;

namespace CredWise_Trail.Models.ViewModels
{
    public class CustomerDashboardViewModel
    {
        public bool HasActiveLoans { get; set; }

        public int ActiveLoanCount { get; set; }
        public decimal TotalPrincipalAmount { get; set; }

        public decimal TotalOutstandingBalance { get; set; }

        public decimal TotalNextPaymentAmount { get; set; }

        public DateTime? EarliestNextDueDate { get; set; }

        public int OverallProgressPercentage { get; set; }

        public List<RecentPaymentItem> RecentPayments { get; set; }

        public CustomerDashboardViewModel()
        {
            RecentPayments = new List<RecentPaymentItem>();
        }
    }

    public class RecentPaymentItem
    {
        public DateTime? PaymentDate { get; set; }
        public string Description { get; set; }
        public string LoanNumber { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; }
    }
}