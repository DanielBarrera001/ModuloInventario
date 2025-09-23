using System.ComponentModel.DataAnnotations;

namespace SistemaInventarioApp.Models
{
    public class RegistroViewModel
    {
        [Required(ErrorMessage = "Error.Requerido")]
        [EmailAddress(ErrorMessage = "Error.Email")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Error.Requerido")]
        public string Password { get; set; }
    }
}
