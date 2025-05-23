using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    public class LoanProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LoanProductId { get; set; }
        [Required]
        [StringLength(50)]
        public string ProductName { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        public decimal InterestRate { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal MinAmount { get; set; }
        [Column(TypeName = "decimal(10, 2)")]
        public decimal MaxAmount { get; set; }
        public int Tenure { get; set; }

        public ICollection<LoanApplication> LoanApplications { get; set; }

    }
}
