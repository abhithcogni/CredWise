namespace CredWise_Trail.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Summary Cards Data:
        public decimal TotalLoanValue { get; set; }
        public int ActiveLoansCount { get; set; }
        public int PendingApplicationsCount { get; set; }
        public int OverdueLoansCount { get; set; }

        // Chart Data:
        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<decimal> NewLoansMonthlyData { get; set; } = new List<decimal>();
        public List<decimal> RepaymentsMonthlyData { get; set; } = new List<decimal>();

        public List<string> LoanStatusLabels { get; set; } = new List<string>();
        public List<int> LoanStatusCounts { get; set; } = new List<int>();

        // Table Data:
        // Using the updated LoanApplication entity directly for the table.
        public List<LoanApplication> RecentLoanApplications { get; set; } = new List<LoanApplication>();
    }
}
