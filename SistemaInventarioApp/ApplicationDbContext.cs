using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp.Entidades;
using System.Threading;


namespace SistemaInventarioApp
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Movimiento> Movimientos { get; set; }
        public DbSet<Producto> Productos { get; set; }
    }
}
