using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    [Table("LOAN_APPLICATIONS")]
    public class LoanApplication
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApplicationId { get; set; } 

        public int CustomerId { get; set; }

        public int? LoanProductId { get; set; }

        [Column(TypeName = "decimal(18, 2)")] 
        public decimal LoanAmount { get; set; } 

        public DateTime ApplicationDate { get; set; }

        public DateTime? ApprovalDate { get; set; } 

        [Required]
        [StringLength(10)]
        public string ApprovalStatus { get; set; } 

        public string LoanProductName { get; set; }


        [Column(TypeName = "decimal(7, 3)")] 
        public decimal InterestRate { get; set; } 

        public int TenureMonths { get; set; } 

        [Column(TypeName = "decimal(18, 2)")]
        public decimal EMI { get; set; } 

        [Column(TypeName = "decimal(18, 2)")]
        public decimal OutstandingBalance { get; set; } 

        public DateTime? NextDueDate { get; set; } 

        public DateTime? LastPaymentDate { get; set; } 

        [Column(TypeName = "decimal(18, 2)")]
        public decimal AmountDue { get; set; } 

        [StringLength(50)]
        public string LoanNumber { get; set; } 

        [StringLength(20)]
        public string LoanStatus { get; set; } = "Pending Disbursement"; 

        public int OverdueMonths { get; set; } = 0; 
        [Column(TypeName = "decimal(18, 2)")]
        public decimal CurrentOverdueAmount { get; set; } = 0; 


        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }

        [ForeignKey("LoanProductId")]
        public LoanProduct LoanProduct { get; set; } 

        //ICollection is used here to represent one-to-many relationship.
        public ICollection<LoanPayment> Payments { get; set; }
        public ICollection<Repayment> Repayments { get; set; }
    }

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
        PENDING_DISBURSEMENT
    }
}