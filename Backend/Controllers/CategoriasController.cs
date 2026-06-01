using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // cualquier usuario autenticado puede ver las categorías
    public class CategoriasController : ControllerBase
    {
        private readonly SqlContext _sqlContext;

        public CategoriasController(SqlContext sqlContext)
        {
            _sqlContext = sqlContext;
        }

        // GET: api/Categorias
        [HttpGet]
        public IActionResult ObtenerCategorias()
        {
            try
            {
                var categorias = _sqlContext.Categorias
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Id)
                    .Select(c => new
                    {
                        c.Id,
                        c.Nombre,
                        c.Descripcion
                    })
                    .ToList();

                return Ok(new { Total = categorias.Count, Categorias = categorias });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener categorías.", Detalle = ex.Message });
            }
        }
    }
}