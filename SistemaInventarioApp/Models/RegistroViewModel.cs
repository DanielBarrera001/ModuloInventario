using System.ComponentModel.DataAnnotations;

namespace SistemaInventarioApp.Models
{
    public class RegistroViewModel
    {
        [Required(ErrorMessage = "Ingresa el correo electronico")]
        [EmailAddress(ErrorMessage = "El campo {0} debe contener un correo valido")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Ingresa la contraseña")]
        public string Password { get; set; }
    }
}
