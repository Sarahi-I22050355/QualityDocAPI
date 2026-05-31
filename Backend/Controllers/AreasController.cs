using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;
using QualityDocAPI.DTOs;
using QualityDocAPI.Models;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "SoloAdmin")]
    public class AreasController : ControllerBase
    {
        private readonly SqlContext _sqlContext;

        public AreasController(SqlContext sqlContext)
        {
            _sqlContext = sqlContext;
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private int? GetIdEmpresaToken()
        {
            var val = User.FindFirst("id_empresa")?.Value;
            return int.TryParse(val, out int e) && e != 0 ? e : (int?)null;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Areas — lista áreas ACTIVAS de la empresa del admin
        // ─────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult ObtenerAreas()
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();

                var areas = _sqlContext.Areas
                    .Where(a => a.Activo && a.IdEmpresa == idEmpresa)
                    .OrderBy(a => a.Nombre)
                    .Select(a => new
                    {
                        a.Id,
                        a.Nombre,
                        a.Descripcion,
                        a.EsGeneral,
                        a.FechaCreacion
                    })
                    .ToList();

                return Ok(new { Total = areas.Count, Areas = areas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener áreas.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Areas/inactivas
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("inactivas")]
        public IActionResult ObtenerAreasInactivas()
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();

                var areas = _sqlContext.Areas
                    .Where(a => !a.Activo && a.IdEmpresa == idEmpresa)
                    .OrderBy(a => a.Nombre)
                    .Select(a => new
                    {
                        a.Id,
                        a.Nombre,
                        a.Descripcion,
                        a.EsGeneral,
                        a.FechaCreacion
                    })
                    .ToList();

                return Ok(new { Total = areas.Count, Areas = areas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener áreas inactivas.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST: api/Areas — crear área dentro de la empresa del admin
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearArea([FromBody] CrearAreaDTO datos)
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();
                if (idEmpresa == null)
                    return BadRequest(new { Mensaje = "No se pudo determinar la empresa." });

                bool existeActiva = _sqlContext.Areas
                    .Any(a => a.Nombre == datos.Nombre && a.Activo && a.IdEmpresa == idEmpresa);
                if (existeActiva)
                    return BadRequest(new { Mensaje = $"Ya existe un área activa con el nombre '{datos.Nombre}'." });

                var inactivaExistente = _sqlContext.Areas
                    .FirstOrDefault(a => a.Nombre == datos.Nombre && !a.Activo && a.IdEmpresa == idEmpresa);
                if (inactivaExistente != null)
                    return BadRequest(new
                    {
                        Mensaje        = $"Existe un área inactiva con el nombre '{datos.Nombre}'. Puedes reactivarla.",
                        IdAreaInactiva = inactivaExistente.Id
                    });

                var nuevaArea = new AreaSQL
                {
                    Nombre      = datos.Nombre,
                    Descripcion = datos.Descripcion,
                    EsGeneral   = datos.EsGeneral,
                    Activo      = true,
                    IdEmpresa   = idEmpresa.Value
                };

                _sqlContext.Areas.Add(nuevaArea);
                await _sqlContext.SaveChangesAsync();

                return Ok(new
                {
                    Mensaje   = "Área creada exitosamente.",
                    IdArea    = nuevaArea.Id,
                    Nombre    = nuevaArea.Nombre,
                    EsGeneral = nuevaArea.EsGeneral
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al crear el área.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT: api/Areas/{id}
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarArea(int id, [FromBody] CrearAreaDTO datos)
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();

                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo || area.IdEmpresa != idEmpresa)
                    return NotFound(new { Mensaje = "Área no encontrada." });

                bool nombreDuplicado = _sqlContext.Areas
                    .Any(a => a.Nombre == datos.Nombre && a.Id != id && a.Activo && a.IdEmpresa == idEmpresa);
                if (nombreDuplicado)
                    return BadRequest(new { Mensaje = $"Ya existe otra área con el nombre '{datos.Nombre}'." });

                area.Nombre      = datos.Nombre;
                area.Descripcion = datos.Descripcion;
                area.EsGeneral   = datos.EsGeneral;

                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = "Área actualizada correctamente.", IdArea = area.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al actualizar el área.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT: api/Areas/{id}/reactivar
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}/reactivar")]
        public async Task<IActionResult> ReactivarArea(int id)
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();

                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || area.IdEmpresa != idEmpresa)
                    return NotFound(new { Mensaje = $"No existe un área con ID {id} en tu empresa." });

                if (area.Activo)
                    return BadRequest(new { Mensaje = $"El área '{area.Nombre}' ya está activa." });

                bool nombreEnUso = _sqlContext.Areas
                    .Any(a => a.Nombre == area.Nombre && a.Activo && a.Id != id && a.IdEmpresa == idEmpresa);
                if (nombreEnUso)
                    return BadRequest(new { Mensaje = $"No se puede reactivar: ya existe otra área activa con ese nombre." });

                area.Activo = true;
                await _sqlContext.SaveChangesAsync();

                return Ok(new
                {
                    Mensaje   = $"Área '{area.Nombre}' reactivada correctamente.",
                    IdArea    = area.Id,
                    Nombre    = area.Nombre,
                    EsGeneral = area.EsGeneral
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al reactivar el área.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // DELETE: api/Areas/{id} — soft delete
        // ─────────────────────────────────────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> DesactivarArea(int id)
        {
            try
            {
                var idEmpresa = GetIdEmpresaToken();

                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo || area.IdEmpresa != idEmpresa)
                    return NotFound(new { Mensaje = "Área no encontrada." });

                if (area.EsGeneral)
                    return BadRequest(new { Mensaje = "No se puede desactivar el área General del sistema." });

                area.Activo = false;
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = $"Área '{area.Nombre}' desactivada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al desactivar el área.", Detalle = ex.Message });
            }
        }
    }
}