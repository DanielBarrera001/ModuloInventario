using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp.Models;
using SistemaInventarioApp.Entidades;

namespace SistemaInventarioApp.Controllers
{
    // CLASES AUXILIARES (iguales a la versión anterior, ProductoVentaDiaria y DashboardViewModel)
    public class ProductoVentaDiaria
    {
        public string NombreProducto { get; set; }
        public int CantidadVendida { get; set; }
    }

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
            var fechaInicioMes = DateTime.Now.AddDays(-30);
            var hoy = DateTime.Today; // Inicio del día para la métrica diaria

            // 1. Productos Stock Bajo (Stock < 15)
            var productosStockBajo = await _context.Productos
                // ?? FILTRO AÑADIDO: Solo incluir productos de tipo Bien (los que tienen Stock)
                .Where(p => p.Tipo == TipoProducto.Bien)
                .Where(p => p.Stock < 15)
                .OrderBy(p => p.Stock)
                .Take(5)
                .ToListAsync();

            // 2. Productos con Mayor Venta HOY
            var productosMayorVentaDiaria = await _context.Movimientos
                .Where(m => m.Tipo == TipoMovimiento.Venta && m.Fecha.Date == hoy)
                .Include(m => m.Producto)
                .GroupBy(m => m.ProductoId)
                .Select(g => new ProductoVentaDiaria
                {
                    NombreProducto = g.First().Producto.Nombre,
                    CantidadVendida = g.Sum(m => m.Cantidad)
                })
                .OrderByDescending(p => p.CantidadVendida)
                .Take(5)
                .ToListAsync();


            // 3. Métricas principales
            var totalProductos = await _context.Productos.CountAsync();

            // ?? CORRECCIÓN CLAVE: Total de Unidades Vendidas (Venta) HOY
            var totalUnidadesVendidasHoy = await _context.Movimientos
                .Where(m => m.Tipo == TipoMovimiento.Venta && m.Fecha.Date == hoy)
                .SumAsync(m => (int?)m.Cantidad) ?? 0; // Usar (int?) y ?? 0 para manejar la suma de un conjunto vacío

            // 4. Movimientos por tipo en los últimos 30 días (Lógica igual, solo para gráficos)
            var movimientosUltimos30Dias = await _context.Movimientos
                .Where(m => m.Fecha >= fechaInicioMes && m.Tipo != TipoMovimiento.NuevoProducto)
                .GroupBy(m => m.Tipo)
                .Select(g => new { Tipo = g.Key, Cantidad = g.Sum(m => m.Cantidad) })
                .ToListAsync();

            var model = new DashboardViewModel
            {
                ProductosStockBajo = productosStockBajo,
                ProductosMayorVentaDiaria = productosMayorVentaDiaria,
                TotalProductos = totalProductos,
                // ?? Asignamos el nuevo cálculo de ventas de hoy
                TotalUnidadesMovidas = totalUnidadesVendidasHoy,

                // Datos para gráficos
                LabelsStock = productosStockBajo.Select(p => p.Nombre).ToList(),
                DataStock = productosStockBajo.Select(p => p.Stock).ToList(),

                LabelsMovimientos = movimientosUltimos30Dias.Select(m => m.Tipo.ToString()).ToList(),
                DataMovimientos = movimientosUltimos30Dias.Select(m => m.Cantidad).ToList()
            };

            return View(model);
        }

        // ... (El resto del código del controlador es el mismo) ...
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

    public class DashboardViewModel
    {
        public List<Producto> ProductosStockBajo { get; set; } = new();
        public List<ProductoVentaDiaria> ProductosMayorVentaDiaria { get; set; } = new();

        public int TotalProductos { get; set; }
        public int TotalUnidadesMovidas { get; set; } = 0; // ¡Usaremos esto para la venta de hoy!

        // Propiedades necesarias para gráficos
        public List<string> LabelsStock { get; set; } = new();
        public List<int> DataStock { get; set; } = new();

        public List<string> LabelsMovimientos { get; set; } = new();
        public List<int> DataMovimientos { get; set; } = new();
    }
}