using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    public class Customer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string Email {  get; set; }

        [Required]
        [StringLength(50)]
        public string PasswordHash { get; set; }

        [StringLength(15)]
        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public ICollection<LoanApplication> LoanApplications { get; set; }
        public ICollection<KycApproval> kycApprovals { get; set; }
    }
}
