using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace CredWise_Trail.Models
{
    public class LoanApplication
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ApplicationId { get; set; }
        public int CustomerId { get; set; }
        public int LoanProductId { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal LoanAmount { get; set; }
        public DateTime ApplicationDate { get; set; }
        [Required]
        [StringLength(10)]
        public string ApprovalStatus { get; set; }

        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }
        [ForeignKey("LoanProductId")]
        public LoanProduct LoanProduct { get; set; }

        public ICollection<Repayment> Repayments { get; set; }

    }
}
