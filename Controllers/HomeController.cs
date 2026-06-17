using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TheConsortiumApp.Data;
using TheConsortiumApp.Models;

namespace TheConsortiumApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        // ✅ UN SOLO CONSTRUCTOR (evita el error de "Multiple constructors...")
        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            string email = HttpContext.Session.GetString("UserEmail");

            if (email == null)
                return RedirectToAction("Login", "Account");

            // ✅ SI ES ADMIN → VE TODO
            if (email == "admin@admin.com")
            {
                ViewBag.TotalEmpresas = await _context.Empresas.CountAsync();
                ViewBag.TotalConsorcios = await _context.Consorcios.CountAsync();
                ViewBag.TotalUnidades = await _context.UnidadesFuncionales.CountAsync();
                ViewBag.UnidadesActivas = ViewBag.TotalUnidades;

                ViewBag.GastosMesActual = await _context.Gastos
                    .Where(g => g.FechaRegistro.Month == DateTime.Today.Month &&
                                g.FechaRegistro.Year == DateTime.Today.Year)
                    .SumAsync(g => (decimal?)g.Monto) ?? 0m;

                return View();
            }

            // ✅ EMPRESA NORMAL → SOLO SUS DATOS
            int? empresaId = HttpContext.Session.GetInt32("EmpresaId");
            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            int year = DateTime.Today.Year;
            int month = DateTime.Today.Month;

            var consorciosIds = await _context.Consorcios
                .Where(c => c.EmpresaId == empresaId.Value)
                .Select(c => c.Id)
                .ToListAsync();

            ViewBag.TotalConsorcios = consorciosIds.Count;

            ViewBag.TotalUnidades = await _context.UnidadesFuncionales
                .CountAsync(u => consorciosIds.Contains(u.ConsorcioId));

            ViewBag.UnidadesActivas = ViewBag.TotalUnidades;

            ViewBag.GastosMesActual = await _context.Gastos
                .Where(g => consorciosIds.Contains(g.ConsorcioId)
                         && g.FechaRegistro.Year == year
                         && g.FechaRegistro.Month == month)
                .SumAsync(g => (decimal?)g.Monto) ?? 0m;

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}