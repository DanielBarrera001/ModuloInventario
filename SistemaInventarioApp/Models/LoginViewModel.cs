using System.ComponentModel.DataAnnotations;

namespace SistemaInventarioApp.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Ingresa el correo electronico")]
        [EmailAddress(ErrorMessage = "El campo {0} debe contener un correo valido")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Ingresa la contraseña")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "Recuerdame")]
        public bool Recuerdame { get; set; }
    }
}
