using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // You might not need this if you're not using DB-specific attributes here, but it's good practice for consistency.

namespace CredWise_Trail.Models.ViewModels
{
    public class LoanApplicationViewModel
    {
        // This is a common practice to map the loan product name from the dropdown
        // to retrieve the actual LoanProductId from the database.
        [Required(ErrorMessage = "Please select a loan product.")]
        [Display(Name = "Loan Product Name")]
        public string LoanProductName { get; set; }

        // Loan amount is required and must be a positive value.
        [Required(ErrorMessage = "Loan amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Loan amount must be greater than zero.")]
        [Column(TypeName = "decimal(18, 2)")] // Ensure precision for decimal type in DB
        [Display(Name = "Loan Amount")]
        public decimal LoanAmount { get; set; }

        // Tenure is required and must be a positive integer.
        [Required(ErrorMessage = "Tenure is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Tenure must be at least 1 month.")]
        [Display(Name = "Tenure (months)")]
        public int Tenure { get; set; }

        // Interest Rate and Total Amount can be calculated on the client side,
        // but for robustness, you might recalculate or validate them on the server side too.
        // For this example, they are not directly submitted by the form but are derived values.
        // If you were to submit them, you would add [Required] and other validations.

        // You might consider adding a property for EstimatedTotalAmount for server-side validation
        // if you want to ensure the client-side calculation matches expectations.
        // public decimal EstimatedTotalAmount { get; set; }
    }
}