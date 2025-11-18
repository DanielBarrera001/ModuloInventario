using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp;
using SistemaInventarioApp.Entidades;
using SistemaInventarioApp.Servicios;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaInventarioApp.Controllers
{
    // Clases Auxiliares para el ViewModel de Estadísticas
    public class ProductoMovimientoVolumen
    {
        public string NombreProducto { get; set; }
        public string CodigoBarras { get; set; }
        public int CantidadMovida { get; set; }
        public int StockActual { get; set; }
    }

    public class EstadisticasMovimientosViewModel
    {
        public List<ProductoMovimientoVolumen> VolumenIngresoNuevo { get; set; } = new();
        public List<ProductoMovimientoVolumen> VolumenReingreso { get; set; } = new();
        public List<ProductoMovimientoVolumen> VolumenSalida { get; set; } = new();
    }


    [Authorize(Roles = Constantes.RolAdmin)]
    public class MovimientosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MovimientosController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: Movimientos/EstadisticasSalida (Actualizado para seleccionar mes)
        public async Task<IActionResult> EstadisticasSalida(DateTime? mesSeleccionado)
        {
            var hoy = DateTime.Today;

            // 1. Determinar el primer y último día del mes a consultar
            // Si no se selecciona mes, por defecto es el primer día del mes actual.
            var mesBase = mesSeleccionado.HasValue ? mesSeleccionado.Value : new DateTime(hoy.Year, hoy.Month, 1);

            var fechaInicio = new DateTime(mesBase.Year, mesBase.Month, 1);
            var fechaFin = fechaInicio.AddMonths(1).AddDays(-1);

            // 2. Ajustar la fecha fin para que no exceda el día de hoy
            if (fechaFin > hoy)
            {
                fechaFin = hoy;
            }

            // Pasar el mes base a la vista para mantener la selección en el dropdown
            ViewData["MesSeleccionado"] = mesBase;

            // Función de ayuda para calcular el volumen por tipo de movimiento en el rango de fechas
            async Task<List<ProductoMovimientoVolumen>> ObtenerVolumen(TipoMovimiento tipo)
            {
                return await _context.Movimientos
                    // Filtra por el rango de fechas calculado
                    .Where(m => m.Tipo == tipo && m.Fecha.Date >= fechaInicio.Date && m.Fecha.Date <= fechaFin.Date)
                    .Include(m => m.Producto)
                    .GroupBy(m => m.ProductoId)
                    .Select(g => new ProductoMovimientoVolumen
                    {
                        NombreProducto = g.First().Producto.Nombre,
                        CodigoBarras = g.First().Producto.CodigoBarras,
                        CantidadMovida = g.Sum(m => m.Cantidad),
                        StockActual = g.First().Producto.Stock
                    })
                    .OrderByDescending(p => p.CantidadMovida) // Ordena de mayor a menor volumen
                    .ToListAsync(); // Obtiene la lista completa de productos que tuvieron movimiento
            }

            var model = new EstadisticasMovimientosViewModel
            {
                VolumenIngresoNuevo = await ObtenerVolumen(TipoMovimiento.NuevoProducto),
                VolumenReingreso = await ObtenerVolumen(TipoMovimiento.Ingreso),
                VolumenSalida = await ObtenerVolumen(TipoMovimiento.Salida)
            };

            return View(model);
        }

        // GET: Movimientos
        public async Task<IActionResult> Index(string search, DateTime? fechaInicio, DateTime? fechaFin, string tipo)
        {
            var movimientosQuery = _context.Movimientos.Include(m => m.Producto).AsQueryable();

            // Validar que las fechas no sean del futuro
            if (fechaInicio.HasValue && fechaInicio.Value.Date > DateTime.Now.Date)
            {
                TempData["Error"] = "La fecha de inicio no puede ser futura.";
                fechaInicio = null;
            }

            if (fechaFin.HasValue && fechaFin.Value.Date > DateTime.Now.Date)
            {
                TempData["Error"] = "La fecha de fin no puede ser futura.";
                fechaFin = null;
            }

            // Validar que fechaFin no sea anterior a fechaInicio
            if (fechaInicio.HasValue && fechaFin.HasValue && fechaFin.Value.Date < fechaInicio.Value.Date)
            {
                TempData["Error"] = "La fecha de fin no puede ser anterior a la fecha de inicio.";
                fechaFin = fechaInicio;
            }

            if (!string.IsNullOrEmpty(search))
            {
                movimientosQuery = movimientosQuery.Where(m =>
                    m.Producto.Nombre.Contains(search) ||
                    m.Producto.CodigoBarras.Contains(search));
            }

            if (fechaInicio.HasValue)
            {
                movimientosQuery = movimientosQuery.Where(m => m.Fecha.Date >= fechaInicio.Value.Date);
            }

            if (fechaFin.HasValue)
            {
                movimientosQuery = movimientosQuery.Where(m => m.Fecha.Date <= fechaFin.Value.Date);
            }

            if (!string.IsNullOrEmpty(tipo))
            {
                if (Enum.TryParse<TipoMovimiento>(tipo, out var tipoEnum))
                {
                    movimientosQuery = movimientosQuery.Where(m => m.Tipo == tipoEnum);
                }
            }

            ViewData["Search"] = search;
            ViewData["FechaInicio"] = fechaInicio?.ToString("yyyy-MM-dd");
            ViewData["FechaFin"] = fechaFin?.ToString("yyyy-MM-dd");
            ViewData["TipoMovimiento"] = tipo;

            var movimientos = await movimientosQuery.OrderByDescending(m => m.Fecha).ToListAsync();
            return View(movimientos);
        }


        // GET: Movimientos/Delete/5
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Delete(int id)
        {
            var movimiento = await _context.Movimientos
                .Include(m => m.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (movimiento == null) return NotFound();

            return View(movimiento);
        }

        // POST: Movimientos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movimiento = await _context.Movimientos.FindAsync(id);
            if (movimiento != null)
            {
                _context.Movimientos.Remove(movimiento);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = Constantes.RolAdmin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarMovimientos(int productoId)
        {
            var producto = await _context.Productos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == productoId);

            if (producto == null)
            {
                TempData["Error"] = "Producto no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (!producto.Movimientos.Any())
            {
                TempData["Info"] = "El producto no tiene movimientos registrados.";
                return RedirectToAction(nameof(Index));
            }

            // Eliminar todos los movimientos
            _context.Movimientos.RemoveRange(producto.Movimientos);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Todos los movimientos del producto {producto.Nombre} fueron eliminados.";
            return RedirectToAction(nameof(Index));
        }

    }
}