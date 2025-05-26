using System.ComponentModel.DataAnnotations;

namespace CredWise_Trail.Models.ViewModels
{
    public class LoginAdminViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)] // Hides input in UI for security
        public string PasswordHash { get; set; }
    }
}
