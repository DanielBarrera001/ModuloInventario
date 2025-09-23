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
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reportes
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reportes/Generar?tipo=diario&fecha=2025-09-22
        public async Task<IActionResult> Generar(string tipo, string fecha, int? productoId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            DateTime fechaSeleccionada;
            if (!DateTime.TryParse(fecha, out fechaSeleccionada))
                fechaSeleccionada = DateTime.Today;

            IQueryable<Movimiento> movimientosQuery = _context.Movimientos
                .Include(m => m.Producto);

            string titulo = "";

            switch (tipo)
            {
                case "diario":
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Fecha.Date == fechaSeleccionada.Date);
                    titulo = $"Reporte Diario {fechaSeleccionada:yyyy-MM-dd}";
                    break;

                case "mensual":
                    movimientosQuery = movimientosQuery
                        .Where(m => m.Fecha.Year == fechaSeleccionada.Year
                                    && m.Fecha.Month == fechaSeleccionada.Month);
                    titulo = $"Reporte Mensual {fechaSeleccionada:MMMM yyyy}";
                    break;

                case "producto":
                    if (productoId.HasValue)
                    {
                        movimientosQuery = movimientosQuery
                            .Where(m => m.ProductoId == productoId.Value);
                        var producto = await _context.Productos.FindAsync(productoId.Value);
                        titulo = $"Reporte Producto: {producto?.Nombre ?? "Desconocido"}";
                    }
                    else
                    {
                        return BadRequest("Debe seleccionar un producto.");
                    }
                    break;

                default:
                    return BadRequest("Tipo de reporte inválido.");
            }

            var movimientos = await movimientosQuery.OrderBy(m => m.Fecha).ToListAsync();

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

                    // Contenido
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
