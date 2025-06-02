using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    [Table("LOAN_PAYMENTS")] // Name of your database table for payments
    public class LoanPayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PaymentId { get; set; }

        // Foreign Key to the Loan Application table
        public int LoanId { get; set; }
        public int CustomerId { get; set; } // For easy lookup/filtering

        [Required]
        [Column(TypeName = "decimal(18, 2)")] // Use decimal for currency to avoid precision issues
        public decimal PaidAmount { get; set; }

        public DateTime? PaymentDate { get; set; } = DateTime.Now; // Default to current time

        [StringLength(50)]
        public string PaymentMethod { get; set; } // e.g., "Saved Bank Account", "New Card"

        [StringLength(255)] // To store transaction IDs from payment gateways
        public string TransactionId { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Success"; // Status of the payment (Success, Failed, Pending)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountDue { get; set; }

        // Navigation property to the LoanApplication model
        // You'll need to make sure your LoanApplication model exists and is correctly named.
        // For example, if your loan application model is named 'LoanApplication'
        // and has a primary key 'LoanId'.
        [ForeignKey("LoanId")]
        public LoanApplication LoanApplication { get; set; }
    }
}