using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp.Models;
using SistemaInventarioApp.Entidades;

namespace SistemaInventarioApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var ultimosProductos = await _context.Productos
                .OrderByDescending(p => p.Id)
                .Take(3)
                .ToListAsync();

            var productosStockBajo = await _context.Productos
                .Where(p => p.Stock < 15)
                .OrderBy(p => p.Stock)
                .Take(5) // solo 5 para el gráfico
                .ToListAsync();

            var totalProductos = await _context.Productos.CountAsync();
            var valorTotalInventario = await _context.Productos
                .SumAsync(p => (p.Precio ?? 0) * p.Stock);

            // Movimientos por tipo
            var movimientosPorTipo = await _context.Movimientos
                .GroupBy(m => m.Tipo)
                .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            var model = new DashboardViewModel
            {
                UltimosProductos = ultimosProductos,
                ProductosStockBajo = productosStockBajo,
                TotalProductos = totalProductos,
                ValorTotalInventario = valorTotalInventario,

                // Incluir Producto para que se muestren todos los movimientos
                UltimosMovimientos = await _context.Movimientos
                    .Include(m => m.Producto)
                    .OrderByDescending(m => m.Fecha)
                    .Take(10)
                    .ToListAsync(),

                // Datos para gráficos
                LabelsStock = productosStockBajo.Select(p => p.Nombre).ToList(),
                DataStock = productosStockBajo.Select(p => p.Stock).ToList(),

                LabelsMovimientos = movimientosPorTipo.Select(m => m.Tipo.ToString()).ToList(),
                DataMovimientos = movimientosPorTipo.Select(m => m.Cantidad).ToList()
            };

            return View(model);
        }




        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string mensaje = null)
        {
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                MensajePersonalizado = mensaje
            };

            return View(model);
        }

    }

    // Modelo de vista para el dashboard
    public class DashboardViewModel
    {
        public List<Producto> UltimosProductos { get; set; } = new();
        public List<Producto> ProductosStockBajo { get; set; } = new();

        public int TotalProductos { get; set; }
        public double ValorTotalInventario { get; set; }

        public List<Movimiento> UltimosMovimientos { get; set; } = new(); // NUEVO

        public List<string> LabelsStock { get; set; } = new();
        public List<int> DataStock { get; set; } = new();

        public List<string> LabelsMovimientos { get; set; } = new();
        public List<int> DataMovimientos { get; set; } = new();

    }

}
