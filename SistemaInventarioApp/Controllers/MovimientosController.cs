using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp;
using SistemaInventarioApp.Entidades;
using SistemaInventarioApp.Servicios;

namespace SistemaInventarioApp.Controllers
{
    public class MovimientosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MovimientosController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: Movimientos
        [Authorize(Roles = Constantes.RolAdmin)]
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
                fechaFin = fechaInicio; // opcional: fijamos fechaFin igual a fechaInicio
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
