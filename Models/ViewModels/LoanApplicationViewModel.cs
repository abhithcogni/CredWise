using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models.ViewModels
{
    public class LoanApplicationViewModel
    {
        [Required(ErrorMessage = "Please select a loan product.")]
        [Display(Name = "Loan Product Name")]
        public string LoanProductName { get; set; }

        [Required(ErrorMessage = "Loan amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Loan amount must be greater than zero.")]
        [Column(TypeName = "decimal(18, 2)")] 
        [Display(Name = "Loan Amount")]
        public decimal LoanAmount { get; set; }

        [Required(ErrorMessage = "Tenure is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Tenure must be at least 1 month.")]
        [Display(Name = "Tenure (months)")]
        public int Tenure { get; set; }
    }
}