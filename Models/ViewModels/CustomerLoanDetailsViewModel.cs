namespace CredWise_Trail.Models.ViewModels
{
    // ViewModel for the main customer table
    public class CustomerViewModel
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public int LoanApplicationCount { get; set; }
    }

    // ViewModel for the detailed customer view in the modal
    public class CustomerDetailViewModel
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string AccountNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public ICollection<LoanApplicationSummaryViewModel> LoanApplications { get; set; }
    }

    // ViewModel for summarizing loan applications in the modal
    public class LoanApplicationSummaryViewModel
    {
        public int ApplicationId { get; set; }
        public string ProductName { get; set; }
        public decimal LoanAmount { get; set; }
        public DateTime ApplicationDate { get; set; }
        public string ApprovalStatus { get; set; }
    }
}