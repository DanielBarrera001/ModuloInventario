using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaInventarioApp;
using SistemaInventarioApp.Entidades;
using SistemaInventarioApp.Servicios;

namespace SistemaInventarioApp.Controllers
{
    public class ProductosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Productos
        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Productos.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.Nombre.Contains(search) ||
                    p.CodigoBarras.Contains(search));
            }

            var productos = await query.ToListAsync();
            return View(productos);
        }

        // GET: Productos/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Productos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                bool codigoExistente = await _context.Productos
                    .AnyAsync(p => p.CodigoBarras == producto.CodigoBarras);

                if (codigoExistente)
                {
                    ModelState.AddModelError("CodigoBarras", "Ya existe un producto con este código de barras.");
                    return View(producto);
                }

                _context.Add(producto);
                await _context.SaveChangesAsync();

                // Registrar movimiento de producto nuevo
                var movimiento = new Movimiento
                {
                    ProductoId = producto.Id,
                    Cantidad = producto.Stock,
                    Tipo = TipoMovimiento.NuevoProducto,
                    Fecha = DateTime.Now
                };
                _context.Movimientos.Add(movimiento);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        [Authorize(Roles = Constantes.RolAdmin)]
        // GET: Productos/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
            return View(producto);
        }

        // POST: Productos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Edit(int id, Producto producto)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var productoOriginal = await _context.Productos.FindAsync(id);
                    if (productoOriginal == null) return NotFound();

                    // Solo actualizar campos editables
                    productoOriginal.Nombre = producto.Nombre;
                    productoOriginal.Descripcion = producto.Descripcion;
                    productoOriginal.Precio = producto.Precio;
                    productoOriginal.Stock = producto.Stock;
                    productoOriginal.CodigoBarras = producto.CodigoBarras;

                    _context.Update(productoOriginal);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Productos.Any(e => e.Id == producto.Id))
                        return NotFound();
                    else
                        throw;
                }
            }
            return View(producto);
        }

        [Authorize(Roles = Constantes.RolAdmin)]
        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null) return NotFound();

            if (producto.Movimientos.Any())
            {
                TempData["ErrorEliminar"] = "No se puede eliminar este producto porque tiene movimientos registrados. Elimine primero los movimientos.";
                return RedirectToAction(nameof(Index));
            }

            return View(producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null) return NotFound();

            if (producto.Movimientos.Any())
            {
                TempData["ErrorEliminar"] = "No se puede eliminar este producto porque tiene movimientos registrados. Elimine primero los movimientos.";
                return RedirectToAction(nameof(Index));
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Productos/Reingreso
        public IActionResult Reingreso()
        {
            return View();
        }

        // POST: Productos/Reingreso
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reingreso(string codigoBarras, int cantidad)
        {
            if (string.IsNullOrEmpty(codigoBarras) || cantidad <= 0)
            {
                ModelState.AddModelError("", "Debe ingresar un código de barras válido y una cantidad mayor a 0.");
                return View();
            }

            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.CodigoBarras == codigoBarras);
            if (producto == null)
            {
                ModelState.AddModelError("", "Producto no encontrado.");
                return View();
            }

            producto.Stock += cantidad;
            _context.Update(producto);
            await _context.SaveChangesAsync();

            // Registrar movimiento de ingreso
            var movimiento = new Movimiento
            {
                ProductoId = producto.Id,
                Cantidad = cantidad,
                Tipo = TipoMovimiento.Ingreso,
                Fecha = DateTime.Now
            };
            _context.Movimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            ViewData["Mensaje"] = $"Se reingresaron {cantidad} unidades al producto {producto.Nombre}.";
            return View(producto);
        }

        // GET: Productos/Salida
        public IActionResult Salida()
        {
            return View();
        }

        // POST: Productos/Salida
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salida(string codigoBarras, int cantidad)
        {
            if (string.IsNullOrEmpty(codigoBarras) || cantidad <= 0)
            {
                ModelState.AddModelError("", "Debe ingresar un código de barras válido y una cantidad mayor a 0.");
                return View();
            }

            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.CodigoBarras == codigoBarras);
            if (producto == null)
            {
                ModelState.AddModelError("", "Producto no encontrado.");
                return View();
            }

            if (producto.Stock < cantidad)
            {
                ModelState.AddModelError("", $"Stock insuficiente. Stock actual: {producto.Stock}");
                return View(producto);
            }

            producto.Stock -= cantidad;
            _context.Update(producto);
            await _context.SaveChangesAsync();

            // Registrar movimiento de salida
            var movimiento = new Movimiento
            {
                ProductoId = producto.Id,
                Cantidad = cantidad,
                Tipo = TipoMovimiento.Salida,
                Fecha = DateTime.Now
            };
            _context.Movimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            ViewData["Mensaje"] = $"Se retiraron {cantidad} unidades del producto {producto.Nombre}.";
            return View(producto);
        }
    }
}
