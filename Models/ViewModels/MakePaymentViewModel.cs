using System;
using System.ComponentModel.DataAnnotations;

namespace CredWise_Trail.ViewModels
{
    public class MakePaymentViewModel
    {
        public int LoanId { get; set; }
        public string ProductName { get; set; }
        public string ShortDescription { get; set; }

        [Display(Name = "Monthly Installment (EMI)")]
        public decimal MonthlyInstallmentAmount { get; set; } // The fixed EMI

        [Display(Name = "Total Amount Due Now")]
        public decimal AmountDue { get; set; } // This includes current EMI + any overdue amounts

        [Display(Name = "Next Payment Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Outstanding Balance")]
        public decimal OutstandingBalance { get; set; }

        [Display(Name = "Minimum Payment")]
        public decimal MinPayment { get; set; } // Typically EMI

        public bool IsPaymentDue { get; set; } // True if DueDate is today or past

        // --- NEW PROPERTIES FOR OVERDUE DISPLAY ---
        public int OverdueMonths { get; set; }
        public decimal CurrentOverdueAmount { get; set; }
        public bool IsOverdue { get; set; } // True if OverdueMonths > 0

        // --- ADDED: Loan Status ---
        public string LoanStatus { get; set; } // To pass the current loan status to the view
    }

    // Data Transfer Object for incoming payment request from frontend (AJAX)
    public class PaymentRequestDto
    {
        [Required]
        public int LoanId { get; set; }
        [Required]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Payment amount must be greater than zero.")]
        public decimal PaymentAmount { get; set; }
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }
        // Add other properties for new payment methods if needed (e.g., CardNumber, ExpiryDate, CVV)
    }
}