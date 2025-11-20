using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaInventarioApp;
using SistemaInventarioApp.Entidades;

namespace SistemaInventarioApp.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Generar(string tipo, string fecha, int? productoId, string fechaInicio, string fechaFin)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            DateTime fechaSeleccionada;
            DateTime? inicioRango = null;
            DateTime? finRango = null;

            if (!string.IsNullOrEmpty(fecha) && DateTime.TryParse(fecha, out fechaSeleccionada))
                ;
            else
                fechaSeleccionada = DateTime.Today;

            if (!string.IsNullOrEmpty(fechaInicio) && DateTime.TryParse(fechaInicio, out DateTime parsedInicio))
                inicioRango = parsedInicio.Date;

            if (!string.IsNullOrEmpty(fechaFin) && DateTime.TryParse(fechaFin, out DateTime parsedFin))
                finRango = parsedFin.Date.AddDays(1).AddSeconds(-1);

            IQueryable<Movimiento> query = _context.Movimientos.Include(m => m.Producto);
            string titulo = "";
            bool esVenta = false;

            switch (tipo)
            {
                case "diario":
                    query = query.Where(m => m.Fecha.Date == fechaSeleccionada.Date);
                    titulo = $"Reporte Diario General {fechaSeleccionada:yyyy-MM-dd}";
                    break;

                case "mensual":
                    query = query.Where(m => m.Fecha.Year == fechaSeleccionada.Year &&
                                             m.Fecha.Month == fechaSeleccionada.Month);
                    titulo = $"Reporte Mensual General {fechaSeleccionada:MMMM yyyy}";
                    break;

                case "producto":
                    if (!productoId.HasValue)
                        return BadRequest("Debe seleccionar un producto.");

                    query = query.Where(m => m.ProductoId == productoId.Value);

                    var prod = await _context.Productos.FindAsync(productoId.Value);
                    titulo = $"Reporte Historial Producto: {prod?.Nombre ?? "Desconocido"}";
                    break;

                case "ventas_dia":
                    esVenta = true;
                    query = query.Where(m => m.Tipo == TipoMovimiento.Venta &&
                                             m.Fecha.Date == fechaSeleccionada.Date);
                    titulo = $"Reporte de Ventas del Día {fechaSeleccionada:yyyy-MM-dd}";
                    break;

                case "ventas_rango":
                    if (!inicioRango.HasValue || !finRango.HasValue)
                        return BadRequest("Debe proporcionar un rango válido.");

                    esVenta = true;
                    query = query.Where(m => m.Tipo == TipoMovimiento.Venta &&
                                             m.Fecha >= inicioRango.Value &&
                                             m.Fecha <= finRango.Value);
                    titulo = $"Reporte de Ventas {inicioRango.Value:yyyy-MM-dd} a {finRango.Value:yyyy-MM-dd}";
                    break;

                case "ventas_producto":
                    if (!productoId.HasValue || !inicioRango.HasValue || !finRango.HasValue)
                        return BadRequest("Debe seleccionar producto y rango.");

                    esVenta = true;
                    query = query.Where(m => m.Tipo == TipoMovimiento.Venta &&
                                             m.ProductoId == productoId &&
                                             m.Fecha >= inicioRango.Value &&
                                             m.Fecha <= finRango.Value);

                    var p = await _context.Productos.FindAsync(productoId.Value);
                    titulo = $"Reporte Ventas Producto {p?.Nombre ?? "Desconocido"} ({inicioRango.Value:yyyy-MM-dd} a {finRango.Value:yyyy-MM-dd})";
                    break;

                default:
                    return BadRequest("Tipo de reporte inválido.");
            }

            var movimientos = await query.OrderBy(m => m.Fecha).ToListAsync();

            if (esVenta)
            {
                return GenerarPdfVentas(titulo, movimientos);
            }

            return GenerarPdfGeneral(titulo, movimientos);
        }


        // -----------------------------------------------------------
        // PDF VENTAS (SIN DTO - usando directamente Movimiento)
        // -----------------------------------------------------------
        private IActionResult GenerarPdfVentas(string titulo, List<Movimiento> ventas)
        {
            var total = ventas.Sum(m => m.Cantidad * m.PrecioUnitarioVenta);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text(titulo).SemiBold().FontSize(18);
                        row.ConstantItem(100).Text(DateTime.Now.ToString("yyyy-MM-dd")).FontSize(10);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Text("Detalle de Ventas:").Bold().FontSize(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);   // Producto
                                c.RelativeColumn(2);   // Código
                                c.RelativeColumn(1.5f);// Fecha
                                c.RelativeColumn(1);   // Cantidad
                                c.RelativeColumn(1);   // Precio U.
                                c.RelativeColumn(1.5f);// Total
                            });

                            table.Header(h =>
                            {
                                h.Cell().Text("Producto").SemiBold();
                                h.Cell().Text("Código").SemiBold();
                                h.Cell().Text("Fecha").SemiBold();
                                h.Cell().Text("Cantidad").AlignRight().SemiBold();
                                h.Cell().Text("Precio U.").AlignRight().SemiBold();
                                h.Cell().Text("Total").AlignRight().SemiBold();
                            });

                            bool alt = false;
                            foreach (var m in ventas)
                            {
                                var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                                alt = !alt;

                                table.Cell().Background(bg).Text(m.Producto.Nombre);
                                table.Cell().Background(bg).Text(m.Producto.CodigoBarras);
                                table.Cell().Background(bg).Text(m.Fecha.ToString("yyyy-MM-dd"));
                                table.Cell().Background(bg).Text(m.Cantidad.ToString()).AlignRight();
                                table.Cell().Background(bg).Text(m.PrecioUnitarioVenta.ToString("C")).AlignRight();
                                table.Cell().Background(bg).Text((m.Cantidad * m.PrecioUnitarioVenta).ToString("C")).AlignRight();
                            }
                        });

                        col.Item().PaddingTop(20).AlignRight()
                           .Text(txt =>
                           {
                               txt.Span("Total Vendido: ").SemiBold().FontSize(14);
                               txt.Span(total.ToString("C")).FontSize(18).FontColor(Colors.Green.Darken2);
                           });
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Sistema Inventario - ");
                        txt.CurrentPageNumber();
                        txt.Span(" / ");
                        txt.TotalPages();
                    });
                });
            });

            return File(pdf.GeneratePdf(), "application/pdf", $"{titulo}.pdf");
        }


        // -----------------------------------------------------------
        // PDF GENERAL (SIN CAMBIOS)
        // -----------------------------------------------------------
        private IActionResult GenerarPdfGeneral(string titulo, List<Movimiento> movimientos)
        {
            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);

                    page.Header().PaddingBottom(10).BorderBottom(1).Row(row =>
                    {
                        row.RelativeItem().Text(titulo).SemiBold().FontSize(18);
                        row.ConstantItem(100).Text(DateTime.Now.ToString("yyyy-MM-dd")).FontSize(10).AlignRight();
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Producto").SemiBold();
                            header.Cell().Text("Código").SemiBold();
                            header.Cell().Text("Cantidad").AlignRight().SemiBold();
                            header.Cell().Text("Tipo").SemiBold();
                            header.Cell().Text("Fecha/Hora").SemiBold();
                        });

                        bool alternate = false;
                        foreach (var m in movimientos)
                        {
                            var bg = alternate ? Colors.Grey.Lighten4 : Colors.White;
                            alternate = !alternate;

                            table.Cell().Background(bg).Text(m.Producto.Nombre);
                            table.Cell().Background(bg).Text(m.Producto.CodigoBarras);
                            table.Cell().Background(bg).Text(m.Cantidad.ToString()).AlignRight();
                            table.Cell().Background(bg).Text(m.Tipo.ToString());
                            table.Cell().Background(bg).Text(m.Fecha.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Sistema Inventario - ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return File(pdf.GeneratePdf(), "application/pdf", $"{titulo}.pdf");
        }
    }
}
