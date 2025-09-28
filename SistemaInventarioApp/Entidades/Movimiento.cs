using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaInventarioApp.Entidades
{

    public enum TipoMovimiento
    {
        Ingreso,
        Salida,
        NuevoProducto 
    }

    public class Movimiento
    {
        public int Id { get; set; }

        [Required]
        public TipoMovimiento Tipo { get; set; } 

        [Required]
        public int Cantidad { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        public int ProductoId { get; set; }

        [ForeignKey("ProductoId")]

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public Producto Producto { get; set; } 
    }
}
