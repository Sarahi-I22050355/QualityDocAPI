using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;
using QualityDocAPI.DTOs;
using QualityDocAPI.Models;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "SoloAdmin")]  // 🔒 Todo este controller es exclusivo del Admin
    public class AreasController : ControllerBase
    {
        private readonly SqlContext _sqlContext;

        public AreasController(SqlContext sqlContext)
        {
            _sqlContext = sqlContext;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Areas
        // Lista todas las áreas activas del sistema
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
        // POST: api/Areas
        // Crear una nueva área
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearArea([FromBody] CrearAreaDTO datos)
        {
            try
            {
                // No permitir duplicados
                bool existe = _sqlContext.Areas.Any(a => a.Nombre == datos.Nombre && a.Activo);
                if (existe)
                    return BadRequest(new { Mensaje = $"Ya existe un área con el nombre '{datos.Nombre}'." });

                var nuevaArea = new AreaSQL
                {
                    Nombre     = datos.Nombre,
                    Descripcion = datos.Descripcion,
                    EsGeneral  = datos.EsGeneral,
                    Activo     = true
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
        // Editar nombre / descripción de un área
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarArea(int id, [FromBody] CrearAreaDTO datos)
        {
            try
            {
                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo)
                    return NotFound(new { Mensaje = "Área no encontrada." });

                // Verificar que el nuevo nombre no conflicte con otra área
                bool nombreDuplicado = _sqlContext.Areas
                    .Any(a => a.Nombre == datos.Nombre && a.Id != id && a.Activo);
                if (nombreDuplicado)
                    return BadRequest(new { Mensaje = $"Ya existe otra área con el nombre '{datos.Nombre}'." });

                area.Nombre     = datos.Nombre;
                area.Descripcion = datos.Descripcion;
                area.EsGeneral  = datos.EsGeneral;

                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = "Área actualizada correctamente.", IdArea = area.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al actualizar el área.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // DELETE: api/Areas/{id}
        // Desactivar un área (soft delete, no se borra físicamente)
        // ─────────────────────────────────────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> DesactivarArea(int id)
        {
            try
            {
                var area = await _sqlContext.Areas.FindAsync(id);
                if (area == null || !area.Activo)
                    return NotFound(new { Mensaje = "Área no encontrada." });

                // No se puede desactivar el área General del sistema
                if (area.EsGeneral)
                    return BadRequest(new { Mensaje = "No se puede desactivar el área General del sistema." });

                area.Activo = false;
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = $"Área '{area.Nombre}' desactivada. Los usuarios asignados conservan su registro." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al desactivar el área.", Detalle = ex.Message });
            }
        }
    }
}