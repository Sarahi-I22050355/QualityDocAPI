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

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Areas
        // Lista todas las áreas ACTIVAS del sistema.
        // ─────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult ObtenerAreas()
        {
            try
            {
                var areas = _sqlContext.Areas
                    .Where(a => a.Activo)
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
        // Lista todas las áreas INACTIVAS (desactivadas).
        // Permite al Admin ver qué áreas puede reactivar.
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("inactivas")]
        public IActionResult ObtenerAreasInactivas()
        {
            try
            {
                var areas = _sqlContext.Areas
                    .Where(a => !a.Activo)
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
        // POST: api/Areas
        // Crear una nueva área.
        // Verifica que no exista otra área activa con el mismo nombre.
        // Si existía una inactiva con el mismo nombre, sugiere reactivarla.
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearArea([FromBody] CrearAreaDTO datos)
        {
            try
            {
                // Verificar duplicado entre áreas activas
                bool existeActiva = _sqlContext.Areas
                    .Any(a => a.Nombre == datos.Nombre && a.Activo);
                if (existeActiva)
                    return BadRequest(new { Mensaje = $"Ya existe un área activa con el nombre '{datos.Nombre}'." });

                // Si existe una inactiva con el mismo nombre, sugerir reactivarla
                var inactivaExistente = _sqlContext.Areas
                    .FirstOrDefault(a => a.Nombre == datos.Nombre && !a.Activo);
                if (inactivaExistente != null)
                    return BadRequest(new
                    {
                        Mensaje     = $"Existe un área inactiva con el nombre '{datos.Nombre}'. Puedes reactivarla en PUT /api/Areas/{inactivaExistente.Id}/reactivar.",
                        IdAreaInactiva = inactivaExistente.Id
                    });

                var nuevaArea = new AreaSQL
                {
                    Nombre      = datos.Nombre,
                    Descripcion = datos.Descripcion,
                    EsGeneral   = datos.EsGeneral,
                    Activo      = true
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
        // Editar nombre y descripción de un área activa.
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarArea(int id, [FromBody] CrearAreaDTO datos)
        {
            try
            {
                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo)
                    return NotFound(new { Mensaje = "Área no encontrada o está inactiva." });

                bool nombreDuplicado = _sqlContext.Areas
                    .Any(a => a.Nombre == datos.Nombre && a.Id != id && a.Activo);
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
        // Reactiva un área que fue desactivada anteriormente.
        //
        // Reglas:
        //   - El área debe existir y estar inactiva.
        //   - No puede haber otra área ACTIVA con el mismo nombre
        //     (por si se creó una nueva mientras estaba inactiva).
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}/reactivar")]
        public async Task<IActionResult> ReactivarArea(int id)
        {
            try
            {
                var area = await _sqlContext.Areas.FindAsync(id);

                if (area == null)
                    return NotFound(new { Mensaje = $"No existe un área con ID {id}." });

                if (area.Activo)
                    return BadRequest(new { Mensaje = $"El área '{area.Nombre}' ya está activa." });

                // Verificar que no haya otra área activa con el mismo nombre
                bool nombreEnUso = _sqlContext.Areas
                    .Any(a => a.Nombre == area.Nombre && a.Activo && a.Id != id);
                if (nombreEnUso)
                    return BadRequest(new
                    {
                        Mensaje = $"No se puede reactivar '{area.Nombre}' porque ya existe otra área activa con ese nombre."
                    });

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
        // DELETE: api/Areas/{id}
        // Desactiva un área (soft delete — no se borra físicamente).
        //
        // Reglas:
        //   - No se puede desactivar el área General del sistema.
        //   - Los usuarios asignados conservan su registro histórico.
        // ─────────────────────────────────────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> DesactivarArea(int id)
        {
            try
            {
                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo)
                    return NotFound(new { Mensaje = "Área no encontrada." });

                if (area.EsGeneral)
                    return BadRequest(new { Mensaje = "No se puede desactivar el área General del sistema." });

                area.Activo = false;
                await _sqlContext.SaveChangesAsync();

                return Ok(new
                {
                    Mensaje = $"Área '{area.Nombre}' desactivada. Puedes reactivarla en PUT /api/Areas/{area.Id}/reactivar."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al desactivar el área.", Detalle = ex.Message });
            }
        }
    }
}