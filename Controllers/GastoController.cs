using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using TheConsortiumApp.Data;
using TheConsortiumApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TheConsortiumApp.Controllers
{
    public class GastoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GastoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper: EmpresaId desde sesión
        private int? GetEmpresaId() => HttpContext.Session.GetInt32("EmpresaId");

        // Helper: obtener mis consorcios
        private async Task<List<Consorcio>> GetMisConsorciosAsync(int empresaId)
        {
            return await _context.Consorcios
                .Where(c => c.EmpresaId == empresaId)
                .OrderBy(c => c.Nombre)
                .AsNoTracking()
                .ToListAsync();
        }

        // ✅ LISTAR + FILTRAR (mes/año/consorcio) + TOTAL (SOLO MIS CONSORCIOS)
        [HttpGet]
        public async Task<IActionResult> Index(int? consorcioId, int? year, int? month)
        {
            int? empresaId = GetEmpresaId();
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            int y = year ?? DateTime.Today.Year;
            int m = month ?? DateTime.Today.Month;

            var misConsorcios = await GetMisConsorciosAsync(empresaId.Value);

            ViewBag.Consorcios = misConsorcios;
            ViewBag.ConsorcioIdSeleccionado = consorcioId ?? 0;
            ViewBag.Year = y;
            ViewBag.Month = m;

            // IDs permitidos para seguridad
            var misConsorciosIds = misConsorcios.Select(c => c.Id).ToList();

            // Query base: SOLO gastos de mis consorcios
            var query = _context.Gastos
                .Include(g => g.Consorcio)
                .Where(g => misConsorciosIds.Contains(g.ConsorcioId))
                .AsQueryable();

            // Filtro mes/año
            query = query.Where(g => g.FechaRegistro.Year == y && g.FechaRegistro.Month == m);

            // Filtro por consorcio
            if (consorcioId.HasValue && consorcioId.Value > 0)
                query = query.Where(g => g.ConsorcioId == consorcioId.Value);

            // Total arriba de tabla
            ViewBag.TotalGastos = await query.SumAsync(g => (decimal?)g.Monto) ?? 0m;

            var gastos = await query
                .OrderByDescending(g => g.FechaRegistro)
                .ToListAsync();

            return View(gastos);
        }

        // ✅ FORM CREAR
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            int? empresaId = GetEmpresaId();
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            ViewBag.Consorcios = await GetMisConsorciosAsync(empresaId.Value);
            return View(new Gasto());
        }

        // ✅ GUARDAR GASTO + FACTURA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Gasto gasto, IFormFile? archivoFactura)
        {
            int? empresaId = GetEmpresaId();
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            bool consorcioValido = await _context.Consorcios
                .AnyAsync(c => c.Id == gasto.ConsorcioId && c.EmpresaId == empresaId.Value);

            if (!consorcioValido)
                ModelState.AddModelError("ConsorcioId", "Seleccione un consorcio válido.");

            // Validación opcional del archivo
            if (archivoFactura != null && archivoFactura.Length > 0)
            {
                var ext = Path.GetExtension(archivoFactura.FileName).ToLowerInvariant();
                var permitidos = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };

                if (!permitidos.Contains(ext))
                    ModelState.AddModelError("", "Solo se permiten archivos PDF o imágenes (.jpg, .jpeg, .png, .webp).");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Consorcios = await GetMisConsorciosAsync(empresaId.Value);
                return View(gasto);
            }

            // Guardar factura si se adjunta archivo
            if (archivoFactura != null && archivoFactura.Length > 0)
            {
                var nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(archivoFactura.FileName);

                if (!Directory.Exists(carpeta))
                    Directory.CreateDirectory(carpeta);

                var ruta = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(ruta, FileMode.Create))
                {
                    await archivoFactura.CopyToAsync(stream);

                gasto.ArchivoFactura = nombreArchivo;
            }

            _context.Gastos.Add(gasto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ✅ ELIMINAR (solo si pertenece a mis consorcios)
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            int? empresaId = GetEmpresaId();
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var gasto = await _context.Gastos
                .Include(g => g.Consorcio)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gasto == null)
                return NotFound();

            if (gasto.Consorcio == null || gasto.Consorcio.EmpresaId != empresaId.Value)
                return Forbid();

            _context.Gastos.Remove(gasto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ✅ EXPORTAR PDF
        [HttpGet]
        public async Task<IActionResult> ExportPdf(int? consorcioId, int? year, int? month)
        {
            int? empresaId = GetEmpresaId();
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            int y = year ?? DateTime.Today.Year;
            int m = month ?? DateTime.Today.Month;

            var misConsorcios = await _context.Consorcios
                .Where(c => c.EmpresaId == empresaId.Value)
                .OrderBy(c => c.Nombre)
                .AsNoTracking()
                .ToListAsync();

            var misConsorciosIds = misConsorcios.Select(c => c.Id).ToList();

            var query = _context.Gastos
                .Include(g => g.Consorcio)
                .Where(g => misConsorciosIds.Contains(g.ConsorcioId))
                .AsQueryable();

            query = query.Where(g => g.FechaRegistro.Year == y && g.FechaRegistro.Month == m);

            string consorcioTitulo = "Todos los consorcios";
            if (consorcioId.HasValue && consorcioId.Value > 0)
            {
                query = query.Where(g => g.ConsorcioId == consorcioId.Value);

                var cons = misConsorcios.FirstOrDefault(c => c.Id == consorcioId.Value);
                if (cons != null) consorcioTitulo = cons.Nombre;
            }

            var gastos = await query
                .OrderByDescending(g => g.FechaRegistro)
                .ToListAsync();

            var total = gastos.Sum(g => g.Monto);

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header().Column(h =>
                    {
                        h.Item().Text("Contreras & Asociados").FontSize(18).SemiBold();
                        h.Item().Text($"Gastos - {y}-{m:D2}").FontSize(14);
                        h.Item().Text($"Consorcio: {consorcioTitulo}").FontSize(11);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Total: ${total:0.00}").FontSize(12).SemiBold();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Concepto
                                columns.RelativeColumn(2); // Categoria
                                columns.RelativeColumn(2); // Monto
                                columns.RelativeColumn(2); // Fecha
                                columns.RelativeColumn(3); // Consorcio
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Concepto").SemiBold();
                                header.Cell().Text("Categoría").SemiBold();
                                header.Cell().Text("Monto").SemiBold();
                                header.Cell().Text("Fecha").SemiBold();
                                header.Cell().Text("Consorcio").SemiBold();
                            });

                            foreach (var g in gastos)
                            {
                                table.Cell().Text(g.Concepto);
                                table.Cell().Text(g.Categoria.ToString());
                                table.Cell().Text($"${g.Monto:0.00}");
                                table.Cell().Text(g.FechaRegistro.ToString("yyyy-MM-dd"));
                                table.Cell().Text(g.Consorcio?.Nombre ?? "");
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text("Sistema de Consorcios");
                });
            }).GeneratePdf();

            var fileName = $"Gastos_{y}-{m:D2}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}