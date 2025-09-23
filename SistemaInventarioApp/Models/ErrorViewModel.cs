namespace SistemaInventarioApp.Models
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public string MensajePersonalizado { get; set; } // nueva propiedad
    }

}
