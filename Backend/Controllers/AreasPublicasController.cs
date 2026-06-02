using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;

namespace QualityDocAPI.Controllers
{
    // Controlador separado para endpoints de áreas accesibles
    // por roles distintos al Admin (Supervisor, etc.)
    [ApiController]
    [Route("api/Areas")]
    [Authorize(Policy = "SubeYAprueba")]
    public class AreasPublicasController : ControllerBase
    {
        private readonly SqlContext _sqlContext;

        public AreasPublicasController(SqlContext sqlContext)
        {
            _sqlContext = sqlContext;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Areas/mis-areas
        // Devuelve las áreas activas de la empresa del usuario autenticado.
        // Accesible para Admin (rol 1) y Supervisor (rol 2).
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("mis-areas")]
        public IActionResult ObtenerAreasParaSubir()
        {
            try
            {
                var empStr = User.FindFirst("id_empresa")?.Value;
                int? idEmpresa = int.TryParse(empStr, out int e) && e != 0 ? e : (int?)null;

                if (idEmpresa == null)
                    return StatusCode(403, new { Mensaje = "No se pudo determinar la empresa." });

                var areas = _sqlContext.Areas
                    .Where(a => a.Activo && a.IdEmpresa == idEmpresa)
                    .OrderBy(a => a.Nombre)
                    .Select(a => new
                    {
                        a.Id,
                        a.Nombre,
                        a.EsGeneral
                    })
                    .ToList();

                return Ok(new { Total = areas.Count, Areas = areas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener áreas.", Detalle = ex.Message });
            }
        }
    }
}