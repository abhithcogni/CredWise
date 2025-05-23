using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    [Table("KYC_APPROVAL")]
    public class KycApproval
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int KycApprovalId { get; set; }

        public int CustomerId { get; set; }

        public DateTime SubmissionDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; }

        public string Comments { get; set; }

        public int? ApprovedByAdminId { get; set; }

        public DateTime? ApprovalDate { get; set; }

        [StringLength(255)] // Corresponds to VARCHAR(255)
        public string DocumentPath { get; set; }

        [ForeignKey("CustomerId")]
        public Customer Customer { get; set; }

        [ForeignKey("ApprovedByAdminId")]
        public Admin ApprovedByAdmin { get; set; }
    }
}
