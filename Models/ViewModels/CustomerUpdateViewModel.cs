using System.ComponentModel.DataAnnotations; // For data annotations like [Required]

namespace CredWise_Trail.ViewModels
{
    public class CustomerUpdateViewModel
    {
        // This is typically needed for updates to identify which record to update
        [Required]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address format.")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required.")]
        [Phone(ErrorMessage = "Invalid Phone Number format.")]
        [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        public string Address { get; set; }

        // We explicitly DO NOT include sensitive fields like PasswordHash, AccountNumber, CreatedDate here.
    }
}