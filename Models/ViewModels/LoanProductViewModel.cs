using System.ComponentModel.DataAnnotations;

namespace CredWise_Trail.Models.ViewModels
{
    public class LoanProductViewModel
    {

        [Required(ErrorMessage = "Product Name is required.")]
        [StringLength(50, ErrorMessage = "Product Name cannot exceed 50 characters.")]
        [Display(Name = "Product Name")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name can only contain letters and spaces.")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "Interest Rate is required.")]
        [Range(0.01, 100.00, ErrorMessage = "Interest Rate must be between 0.01% and 100%.")]
        [Display(Name = "Interest Rate")]
        public decimal InterestRate { get; set; }

        [Required(ErrorMessage = "Minimum Loan Amount is required.")]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Minimum Amount must be a positive value.")] // Cast to double for Range attribute
        [Display(Name = "Minimum Loan Amount")]
        public decimal MinAmount { get; set; }

        [Required(ErrorMessage = "Maximum Loan Amount is required.")]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Maximum Amount must be a positive value.")] // Cast to double for Range attribute
        [Display(Name = "Maximum Loan Amount")]
        public decimal MaxAmount { get; set; }

        [Required(ErrorMessage = "Tenure is required.")]
        [Range(1, 1000, ErrorMessage = "Tenure must be between 1 and 1000 months.")]
        [Display(Name = "Maximum Tenure")]
        public int Tenure { get; set; }

    }
}
