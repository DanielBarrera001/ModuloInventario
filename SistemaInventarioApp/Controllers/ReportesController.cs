using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaInventarioApp;
using SistemaInventarioApp.Entidades;
using System.Globalization;
using System.Text;

namespace SistemaInventarioApp.Controllers
{
    // Modelo explícito para el detalle de ventas
    public record DetalleVentaDto(
        string NombreProducto,
        string CodigoBarras,
        int CantidadVendida,
        DateTime Fecha
    );

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


            IQueryable<Movimiento> movimientosQuery = _context.Movimientos
                .Include(m => m.Producto);

            string titulo = "";
            bool esReporteVentas = false;

            switch (tipo)
            {
                case "diario":
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Fecha.Date == fechaSeleccionada.Date);
                    titulo = $"Reporte Diario General {fechaSeleccionada:yyyy-MM-dd}";
                    break;

                case "mensual":
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Fecha.Year == fechaSeleccionada.Year
                                    && m.Fecha.Month == fechaSeleccionada.Month);
                    titulo = $"Reporte Mensual General {fechaSeleccionada:MMMM yyyy}";
                    break;

                case "producto":
                    if (productoId.HasValue)
                    {
                        movimientosQuery = movimientosQuery
                            .Where(m => m.ProductoId == productoId.Value);

                        // Error CS0136: Se renombra la variable a 'productoGeneral'
                        var productoGeneral = await _context.Productos.FindAsync(productoId.Value);
                        titulo = $"Reporte Historial Producto: {productoGeneral?.Nombre ?? "Desconocido"}";
                    }
                    else
                    {
                        return BadRequest("Debe seleccionar un producto.");
                    }
                    break;

                case "ventas_dia":
                    esReporteVentas = true;
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Tipo == TipoMovimiento.Venta)
                        .Where(m => m.Fecha.Date == fechaSeleccionada.Date);
                    titulo = $"Reporte de Ventas del Día {fechaSeleccionada:yyyy-MM-dd}";
                    break;

                case "ventas_rango":
                    if (!inicioRango.HasValue || !finRango.HasValue)
                    {
                        return BadRequest("Debe proporcionar un rango de fechas válido.");
                    }
                    esReporteVentas = true;
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Tipo == TipoMovimiento.Venta)
                        .Where(m => m.Fecha >= inicioRango.Value && m.Fecha <= finRango.Value);
                    titulo = $"Reporte de Ventas: {inicioRango.Value:yyyy-MM-dd} a {finRango.Value:yyyy-MM-dd}";
                    break;

                case "ventas_producto":
                    if (!productoId.HasValue || !inicioRango.HasValue || !finRango.HasValue)
                    {
                        return BadRequest("Debe seleccionar un producto y un rango de fechas.");
                    }
                    esReporteVentas = true;
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Tipo == TipoMovimiento.Venta)
                        .Where(m => m.ProductoId == productoId.Value)
                        .Where(m => m.Fecha >= inicioRango.Value && m.Fecha <= finRango.Value);

                    // Error CS0136: Se renombra la variable a 'productoVenta'
                    var productoVenta = await _context.Productos.FindAsync(productoId.Value);
                    titulo = $"Reporte de Ventas Producto: {productoVenta?.Nombre ?? "Desconocido"} ({inicioRango.Value:yyyy-MM-dd} a {finRango.Value:yyyy-MM-dd})";
                    break;

                default:
                    return BadRequest("Tipo de reporte inválido.");
            }

            var movimientos = await movimientosQuery.OrderBy(m => m.Fecha).ToListAsync();

            if (esReporteVentas)
            {
                var totalUnidadesVendidas = movimientos.Sum(m => m.Cantidad);

                // Se proyecta el resultado al tipo DetalleVentaDto explícito para corregir CS1503.
                var detalleVentas = movimientos
                    .GroupBy(m => new { m.ProductoId, m.Producto.Nombre, m.Producto.CodigoBarras, m.Fecha.Date })
                    .Select(g => new DetalleVentaDto(
                        NombreProducto: g.Key.Nombre,
                        CodigoBarras: g.Key.CodigoBarras,
                        CantidadVendida: g.Sum(m => m.Cantidad),
                        Fecha: g.Key.Date
                    ))
                    .OrderBy(x => x.Fecha)
                    .ThenBy(x => x.NombreProducto)
                    .ToList();

                return GenerarPdfVentas(titulo, detalleVentas, totalUnidadesVendidas);
            }

            return GenerarPdfGeneral(titulo, movimientos);
        }

        private IActionResult GenerarPdfVentas(string titulo, List<DetalleVentaDto> detalleVentas, int totalUnidades)
        {
            var pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);

                    // Cabecera
                    page.Header()
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .BorderColor(Colors.Blue.Medium)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(titulo)
                                .SemiBold()
                                .FontSize(18)
                                .FontColor(Colors.Blue.Medium);

                            row.ConstantItem(100)
                                .Text(DateTime.Now.ToString("yyyy-MM-dd"))
                                .FontSize(10)
                                .AlignRight();
                        });

                    // Contenido: Tabla de resumen de ventas
                    page.Content().PaddingVertical(5).Column(column =>
                    {
                             column.Item()
                            .PaddingBottom(5)
                            .Text("Detalle de las Ventas:")
                            .Bold()
                            .FontSize(12);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Producto
                                columns.RelativeColumn(2); // Código
                                columns.RelativeColumn(2); // Fecha
                                columns.RelativeColumn(1); // Cantidad
                            });

                            // Encabezado de la tabla (Modificado)
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Producto").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Código").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Fecha").SemiBold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Unidades").SemiBold().AlignRight();
                            });

                            bool alternate = false;
                            foreach (var venta in detalleVentas)
                            {
                                var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                                alternate = !alternate;

                                // Corrección para CS1973: Se garantiza que el argumento sea estático o se use la sintaxis de Text() sin extensiones dinámicas
                                table.Cell().Background(bgColor).Padding(5).Text(venta.NombreProducto);
                                table.Cell().Background(bgColor).Padding(5).Text(venta.CodigoBarras);
                                table.Cell().Background(bgColor).Padding(5).Text(venta.Fecha.ToString("yyyy-MM-dd"));
                                table.Cell().Background(bgColor).Padding(5).Text(venta.CantidadVendida.ToString()).AlignRight();
                            }
                        });

                        // RESUMEN DE TOTALES
                        column.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Medium).Column(totalColumn =>
                        {
                            totalColumn.Item().AlignRight().Text(text =>
                            {
                                text.Span("Total de Unidades Vendidas: ").SemiBold();
                                text.Span(totalUnidades.ToString()).FontSize(14).FontColor(Colors.Red.Darken1);
                            });
                        });
                    });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sistema Inventario - ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            var pdfBytes = pdf.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"{titulo}.pdf");
        }


        private IActionResult GenerarPdfGeneral(string titulo, List<Movimiento> movimientos)
        {
            var pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);

                    // Cabecera
                    page.Header()
                        // Corrección para CS1929: Mover PaddingBottom al contenedor (IContainer)
                        .PaddingBottom(10)
                        .BorderBottom(1)
                        .BorderColor(Colors.Grey.Medium)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(titulo)
                                .SemiBold()
                                .FontSize(18)
                                .FontColor(Colors.Blue.Medium);

                            row.ConstantItem(100)
                                .Text(DateTime.Now.ToString("yyyy-MM-dd"))
                                .FontSize(10)
                                .AlignRight();
                        });

                    // Contenido: Tabla de movimientos generales
                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Producto
                            columns.RelativeColumn(2); // Código
                            columns.RelativeColumn(1); // Cantidad
                            columns.RelativeColumn(2); // Tipo
                            columns.RelativeColumn(3); // Fecha
                        });

                        // Encabezado de la tabla
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Producto").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Código").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cantidad").SemiBold().AlignRight();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Tipo").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Fecha/Hora").SemiBold();
                        });

                        bool alternate = false;
                        foreach (var m in movimientos)
                        {
                            var bgColor = alternate ? Colors.Grey.Lighten4 : Colors.White;
                            alternate = !alternate;

                            table.Cell().Background(bgColor).Padding(5).Text(m.Producto.Nombre);
                            table.Cell().Background(bgColor).Padding(5).Text(m.Producto.CodigoBarras);
                            table.Cell().Background(bgColor).Padding(5).Text(m.Cantidad.ToString()).AlignRight();
                            table.Cell().Background(bgColor).Padding(5).Text(m.Tipo switch
                            {
                                TipoMovimiento.NuevoProducto => "Nuevo Producto",
                                TipoMovimiento.Ingreso => "Ingreso",
                                TipoMovimiento.Salida => "Salida",
                                _ => m.Tipo.ToString()
                            });
                            table.Cell().Background(bgColor).Padding(5).Text(m.Fecha.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                    });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sistema Inventario - ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            var pdfBytes = pdf.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"{titulo}.pdf");
        }
    }
}