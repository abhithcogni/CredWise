using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CredWise_Trail.Models
{
    [Table("Admin")]
    public class Admin
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdminId { get; set; }
        [Required]
        [StringLength(50)] 
        public string Username { get; set; }
        [Required]
        [StringLength(50)]
        public string PasswordHash { get; set; }
        [StringLength(100)] 
        public string Email { get; set; }

        [StringLength(50)] 
        public string Role { get; set; }

        public ICollection<KycApproval> ApprovedKycApprovals { get; set; }

    }
}
