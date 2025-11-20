using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp.Entidades; // Importante para usar TipoProducto

namespace SistemaInventarioApp.Entidades
{
    [Index(nameof(CodigoBarras), IsUnique = true)]
    public class Producto
    {
        public int Id { get; set; }


        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public double? Precio { get; set; }

        // NUEVO CAMPO: Tipo de producto/servicio
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        [Display(Name = "Tipo de Elemento")]
        public TipoProducto Tipo { get; set; }

        // El stock ahora está condicionado por el Tipo (solo relevante para Bienes)
        public int Stock { get; set; }

        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string CodigoBarras { get; set; }

        // Propiedad de navegación para los movimientos
        public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
    }
}