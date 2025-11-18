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
    public class ProductosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // -------------------------------------------------------------------
        // 🚀 MÉTODO INDEX ACTUALIZADO CON FILTROS (search, tipo, stock)
        // -------------------------------------------------------------------
        public async Task<IActionResult> Index(
            string search,
            string tipo, // Nuevo parámetro para Tipo de Producto
            string stock // Nuevo parámetro para Estado de Stock
        )
        {
            var productosQuery = _context.Productos.AsQueryable();

            // 1. FILTRO DE BÚSQUEDA POR NOMBRE/CÓDIGO (search)
            if (!string.IsNullOrEmpty(search))
            {
                // Usamos ToLower() para búsquedas case-insensitive
                string searchLower = search.ToLower();
                productosQuery = productosQuery.Where(p =>
                    p.Nombre.ToLower().Contains(searchLower) ||
                    p.CodigoBarras.ToLower().Contains(searchLower)
                );
            }

            // 2. FILTRO POR TIPO DE PRODUCTO (tipo)
            if (!string.IsNullOrEmpty(tipo) && Enum.TryParse(tipo, true, out TipoProducto tipoFiltro))
            {
                productosQuery = productosQuery.Where(p => p.Tipo == tipoFiltro);
            }

            // 3. FILTRO POR ESTADO DE STOCK (stock)
            if (!string.IsNullOrEmpty(stock))
            {
                // Solo se aplica el filtro de Stock a productos de tipo Bien (inventariables)
                productosQuery = productosQuery.Where(p => p.Tipo == TipoProducto.Bien);

                switch (stock.ToLower())
                {
                    case "bajo":
                        // Stock Bajo: > 0 y < 10 unidades
                        productosQuery = productosQuery.Where(p => p.Stock > 0 && p.Stock < 10);
                        break;
                    case "suficiente":
                        // Stock Suficiente: 10 unidades o más
                        productosQuery = productosQuery.Where(p => p.Stock >= 10);
                        break;
                    case "agotado":
                        // Sin Stock: Exactamente 0 unidades
                        productosQuery = productosQuery.Where(p => p.Stock == 0);
                        break;
                }
            }
            // -------------------------------------------------------------------

            var productos = await productosQuery.OrderBy(p => p.Nombre).ToListAsync();

            return View(productos);
        }

        public async Task<IActionResult> StockBajoCompleto(string search)
        {
            var productosQuery = _context.Productos.AsQueryable();

            // 🌟 FILTRO AÑADIDO: Solo mostrar productos de tipo Bien y con Stock bajo
            productosQuery = productosQuery
                .Where(p => p.Tipo == TipoProducto.Bien)
                .Where(p => p.Stock < 15); // Añadimos el criterio de Stock Bajo aquí también

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                productosQuery = productosQuery.Where(p =>
                    p.Nombre.ToLower().Contains(searchLower) ||
                    p.CodigoBarras.ToLower().Contains(searchLower)
                );
            }

            var productos = await productosQuery
                .OrderBy(p => p.Stock)
                .ToListAsync();

            return View(productos);
        }

        [Authorize(Roles = Constantes.RolAdmin)]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                var existeProducto = await _context.Productos
                    .AnyAsync(p => p.CodigoBarras == producto.CodigoBarras);

                if (existeProducto)
                {
                    ModelState.AddModelError("CodigoBarras", "Ya existe un producto con este Código de Barras.");
                    return View(producto);
                }

                if (producto.Tipo == TipoProducto.Servicio)
                {
                    producto.Stock = 0;
                }

                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                if (producto.Tipo == TipoProducto.Bien && producto.Stock > 0)
                {
                    var movimiento = new Movimiento
                    {
                        ProductoId = producto.Id,
                        Cantidad = producto.Stock,
                        Tipo = TipoMovimiento.NuevoProducto,
                        Fecha = DateTime.Now
                    };
                    _context.Movimientos.Add(movimiento);
                    await _context.SaveChangesAsync();
                }

                TempData["Mensaje"] = $"El producto '{producto.Nombre}' fue creado exitosamente.";
                return RedirectToAction(nameof(Index));
            }

            return View(producto);
        }

        public IActionResult Reingreso()
        {
            return View();
        }

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

            if (producto.Tipo == TipoProducto.Servicio)
            {
                ModelState.AddModelError("", $"No se puede ingresar cantidad. El producto '{producto.Nombre}' es un Servicio y no gestiona Stock.");
                return View(producto);
            }

            if (!producto.Activo)
            {
                ModelState.AddModelError("", $"No se puede reingresar stock. El producto '{producto.Nombre}' está Inactivo.");
                return View(producto);
            }

            producto.Stock += cantidad;
            _context.Update(producto);
            await _context.SaveChangesAsync();

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

        public IActionResult Venta()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Venta(string codigoBarras, int cantidad)
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

            if (!producto.Activo)
            {
                ModelState.AddModelError("", $"No se puede registrar la venta. El producto '{producto.Nombre}' está Inactivo.");
                return View(producto);
            }

            if (producto.Tipo == TipoProducto.Bien)
            {
                if (producto.Stock < cantidad)
                {
                    ModelState.AddModelError("", $"Stock insuficiente. Stock actual: {producto.Stock}");
                    return View(producto);
                }

                producto.Stock -= cantidad;
                _context.Update(producto);
            }

            await _context.SaveChangesAsync();

            var movimiento = new Movimiento
            {
                ProductoId = producto.Id,
                Cantidad = cantidad,
                Tipo = TipoMovimiento.Venta,
                Fecha = DateTime.Now
            };
            _context.Movimientos.Add(movimiento);
            await _context.SaveChangesAsync();

            ViewData["Mensaje"] = $"Se registró la venta de {cantidad} unidad(es) de {producto.Nombre}.";
            return View(producto);
        }

        public IActionResult Salida()
        {
            return View();
        }

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

            if (producto.Tipo == TipoProducto.Servicio)
            {
                ModelState.AddModelError("", $"No se puede dar salida de stock. El producto '{producto.Nombre}' es un Servicio y no gestiona Stock.");
                return View(producto);
            }

            if (!producto.Activo)
            {
                ModelState.AddModelError("", $"No se puede dar salida a un producto que está Inactivo.");
                return View(producto);
            }

            if (producto.Stock < cantidad)
            {
                ModelState.AddModelError("", $"Stock insuficiente. Stock actual: {producto.Stock}");
                return View(producto);
            }

            producto.Stock -= cantidad;
            _context.Update(producto);
            await _context.SaveChangesAsync();

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

        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            return View(producto);
        }

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
                    var existeCodigoBarras = await _context.Productos
                        .AnyAsync(p => p.CodigoBarras == producto.CodigoBarras && p.Id != id);

                    if (existeCodigoBarras)
                    {
                        ModelState.AddModelError("CodigoBarras", "Ya existe otro producto con este Código de Barras.");
                        return View(producto);
                    }

                    if (producto.Tipo == TipoProducto.Servicio)
                    {
                        producto.Stock = 0;
                    }

                    _context.Update(producto);
                    await _context.SaveChangesAsync();

                    TempData["Mensaje"] = $"El producto '{producto.Nombre}' fue actualizado exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Productos.Any(e => e.Id == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _context.Productos
                .FirstOrDefaultAsync(m => m.Id == id);

            if (producto == null) return NotFound();

            var movimientosCount = await _context.Movimientos.CountAsync(m => m.ProductoId == id);
            ViewData["MovimientosCount"] = movimientosCount;

            return View(producto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Constantes.RolAdmin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Movimientos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
            {
                TempData["ErrorEliminar"] = "El producto no fue encontrado para su eliminación.";
                return RedirectToAction(nameof(Index));
            }

            if (producto.Movimientos.Any())
            {
                _context.Movimientos.RemoveRange(producto.Movimientos);
            }

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"El producto '{producto.Nombre}' ha sido eliminado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}