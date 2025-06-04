using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; // Added for ICollection

namespace CredWise_Trail.Models // Ensure this namespace matches your project structure
{
    [Table("LOAN_APPLICATIONS")] // Explicitly setting table name, if not already done by convention
    public class LoanApplication
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApplicationId { get; set; } // Your existing primary key name

        public int CustomerId { get; set; }

        public int LoanProductId { get; set; }

        [Column(TypeName = "decimal(18, 2)")] // Changed from 10,2 to 18,2 for consistency with other financial fields
        public decimal LoanAmount { get; set; } // This is the principal amount requested

        public DateTime ApplicationDate { get; set; }

        public DateTime? ApprovalDate { get; set; } // Nullable DateTime for approval date

        [Required]
        [StringLength(10)]
        public string ApprovalStatus { get; set; } // e.g., "PENDING", "APPROVED", "REJECTED"

        // --- PROPERTIES FOR PAYMENT TRACKING ---

        [Column(TypeName = "decimal(7, 3)")] // For interest rate, e.g., 0.0800 for 8%
        public decimal InterestRate { get; set; } // Annual interest rate of the approved loan product

        public int TenureMonths { get; set; } // Loan tenure in months

        [Column(TypeName = "decimal(18, 2)")]
        public decimal EMI { get; set; } // Calculated Equated Monthly Installment for this loan (fixed monthly payment)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal OutstandingBalance { get; set; } // Remaining principal balance + accrued interest

        public DateTime? NextDueDate { get; set; } // When the next payment is due

        public DateTime? LastPaymentDate { get; set; } // When the last payment was made

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountDue { get; set; } // The total amount currently expected for payment (EMI + overdue)

        [StringLength(50)]
        public string LoanNumber { get; set; } // A unique identifier for the approved loan itself (e.g., HL05682)

        [StringLength(20)]
        public string LoanStatus { get; set; } = "Pending Disbursement"; // Overall loan status: "Active", "Closed", "Overdue", etc.

        // --- NEW PROPERTIES FOR OVERDUE TRACKING ---
        public int OverdueMonths { get; set; } = 0; // Number of months loan is overdue
        [Column(TypeName = "decimal(18, 2)")]
        public decimal CurrentOverdueAmount { get; set; } = 0; // Total accumulated overdue amount (principal + interest + penalties)


        // --- EXISTING NAVIGATION PROPERTIES ---

        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }

        [ForeignKey("LoanProductId")]
        public LoanProduct LoanProduct { get; set; } // Assuming this model holds details like interest rates for products

        // --- NEW NAVIGATION PROPERTIES ---

        // This will be for the individual payment records associated with this loan
        public ICollection<LoanPayment> Payments { get; set; } // Changed from Repayments to Payments for consistency with LoanPayment model
    }

    // You might want to define enums for ApprovalStatus and LoanStatus for strong typing in C#.
    // Example:
    public enum LoanApprovalStatus
    {
        PENDING,
        APPROVED,
        REJECTED
    }

    public enum LoanOverallStatus
    {
        ACTIVE,
        CLOSED,
        OVERDUE,
        PENDING_DISBURSEMENT // For loans approved but not yet active
    }
}