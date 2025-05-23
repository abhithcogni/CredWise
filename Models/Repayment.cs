using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    public class Repayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RepaymentId { get; set; }
        public int ApplicationId { get; set; }
        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal AmountDue { get; set; }
        public DateTime? PaymentDate { get; set; }
        [Required]
        [StringLength(10)]
        public string PaymentStatus { get; set; }

        [ForeignKey("ApplicationId")]
        public LoanApplication LoanApplication { get; set; }

    }
}
