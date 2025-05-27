using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    [Table("KYC_APPROVAL")]
    public class KycApproval
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int KycID { get; set; } // Changed from KycApprovalId to KycID

        public int CustomerId { get; set; }

        public DateTime SubmissionDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Default Status to "Pending"

        public DateTime? ApprovalDate { get; set; } 

        [StringLength(255)] // Corresponds to VARCHAR(255)
        public string DocumentPath { get; set; }

        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }

        // Removed ApprovedByAdmin ForeignKey
        // Removed ApprovedByAdmin navigation property
    }
}