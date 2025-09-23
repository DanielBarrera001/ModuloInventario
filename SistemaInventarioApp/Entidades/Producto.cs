using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SistemaInventarioApp.Entidades
{
    [Index(nameof(CodigoBarras), IsUnique = true)]
    public class Producto
    {
        public int Id { get; set; }

        [StringLength(250)]
        [Required]
        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        [Required]
        public double? Precio { get; set; }

        public int Stock { get; set; }

        [Required]
        public string CodigoBarras { get; set; }

        public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();

    }
}
