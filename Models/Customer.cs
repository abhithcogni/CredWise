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
        public string Email { get; set; }

        [Required]
        [StringLength(255)] // Sufficient length for hashed passwords
        public string PasswordHash { get; set; }

        [StringLength(15)]
        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        [StringLength(20)] // Choose an appropriate length for your account numbers
        public string AccountNumber { get; set; }

        // New property for Created Date
        [Required] // This field should always be present
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Set default to current UTC time

        public ICollection<LoanApplication> LoanApplications { get; set; }
        public ICollection<KycApproval> kycApprovals { get; set; }
    }
}