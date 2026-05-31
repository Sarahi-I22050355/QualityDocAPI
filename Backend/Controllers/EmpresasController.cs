using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;
using QualityDocAPI.Models;
using Microsoft.Data.SqlClient;
using BCrypt.Net;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "EsSuperAdmin")]
    public class EmpresasController : ControllerBase
    {
        private readonly SqlContext    _sqlContext;
        private readonly IConfiguration _config;

        public EmpresasController(SqlContext sqlContext, IConfiguration config)
        {
            _sqlContext = sqlContext;
            _config     = config;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Empresas
        // Lista todas las empresas del sistema.
        // ─────────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult ObtenerEmpresas()
        {
            try
            {
                var empresas = _sqlContext.Empresas
                    .OrderBy(e => e.Nombre)
                    .Select(e => new
                    {
                        e.Id,
                        e.Nombre,
                        e.Rfc,
                        e.Activo,
                        e.FechaCreacion
                    })
                    .ToList();

                return Ok(new { Total = empresas.Count, Empresas = empresas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener empresas.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST: api/Empresas
        // Crear una nueva empresa.
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CrearEmpresa([FromBody] CrearEmpresaDTO datos)
        {
            try
            {
                bool existe = _sqlContext.Empresas.Any(e => e.Nombre == datos.Nombre && e.Activo);
                if (existe)
                    return BadRequest(new { Mensaje = $"Ya existe una empresa con el nombre '{datos.Nombre}'." });

                var nuevaEmpresa = new EmpresaSQL
                {
                    Nombre = datos.Nombre,
                    Rfc    = datos.Rfc,
                    Activo = true
                };

                _sqlContext.Empresas.Add(nuevaEmpresa);
                await _sqlContext.SaveChangesAsync();

                return Ok(new
                {
                    Mensaje   = "Empresa creada exitosamente.",
                    IdEmpresa = nuevaEmpresa.Id,
                    Nombre    = nuevaEmpresa.Nombre
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al crear empresa.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT: api/Empresas/{id}/desactivar
        // ─────────────────────────────────────────────────────────────────
        [HttpPut("{id}/desactivar")]
        public async Task<IActionResult> DesactivarEmpresa(int id)
        {
            try
            {
                var empresa = await _sqlContext.Empresas.FindAsync(id);
                if (empresa == null || !empresa.Activo)
                    return NotFound(new { Mensaje = "Empresa no encontrada." });

                empresa.Activo = false;
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = $"Empresa '{empresa.Nombre}' desactivada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al desactivar empresa.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST: api/Empresas/{idEmpresa}/admin
        // Crea el admin (rol 1) para una empresa específica.
        // También crea el área General de esa empresa automáticamente.
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("{idEmpresa}/admin")]
        public async Task<IActionResult> CrearAdminEmpresa(int idEmpresa, [FromBody] CrearAdminEmpresaDTO datos)
        {
            try
            {
                var empresa = await _sqlContext.Empresas.FindAsync(idEmpresa);
                if (empresa == null || !empresa.Activo)
                    return NotFound(new { Mensaje = "Empresa no encontrada o inactiva." });

                // Verificar que no exista ya un usuario con ese email
                bool emailExiste;
                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using var cmd = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE email = @email", con);
                    cmd.Parameters.AddWithValue("@email", datos.Email);
                    emailExiste = (int)cmd.ExecuteScalar() > 0;
                }
                if (emailExiste)
                    return BadRequest(new { Mensaje = $"El email '{datos.Email}' ya está registrado." });

                // Crear área General para la empresa si no existe
                bool tieneAreaGeneral = _sqlContext.Areas
                    .Any(a => a.IdEmpresa == idEmpresa && a.EsGeneral && a.Activo);

                int idAreaGeneral;
                if (!tieneAreaGeneral)
                {
                    var areaGeneral = new AreaSQL
                    {
                        Nombre     = "General",
                        Descripcion = "Área general de la empresa — visible para todos",
                        EsGeneral  = true,
                        Activo     = true,
                        IdEmpresa  = idEmpresa
                    };
                    _sqlContext.Areas.Add(areaGeneral);
                    await _sqlContext.SaveChangesAsync();
                    idAreaGeneral = areaGeneral.Id;
                }
                else
                {
                    idAreaGeneral = _sqlContext.Areas
                        .First(a => a.IdEmpresa == idEmpresa && a.EsGeneral && a.Activo).Id;
                }

                // Crear el admin en la empresa
                string hash = BCrypt.Net.BCrypt.HashPassword(datos.Password);
                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    string insert = @"
                        INSERT INTO Usuarios (id_rol, id_area, id_empresa, nombre_completo, email, password_hash)
                        VALUES (1, @idArea, @idEmpresa, @nombre, @email, @pass)";
                    using var cmd = new SqlCommand(insert, con);
                    cmd.Parameters.AddWithValue("@idArea",    idAreaGeneral);
                    cmd.Parameters.AddWithValue("@idEmpresa", idEmpresa);
                    cmd.Parameters.AddWithValue("@nombre",    datos.NombreCompleto);
                    cmd.Parameters.AddWithValue("@email",     datos.Email);
                    cmd.Parameters.AddWithValue("@pass",      hash);
                    cmd.ExecuteNonQuery();
                }

                return Ok(new
                {
                    Mensaje  = $"Admin creado para la empresa '{empresa.Nombre}'.",
                    Email    = datos.Email,
                    Empresa  = empresa.Nombre
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al crear admin.", Detalle = ex.Message });
            }
        }
    }

    // ── DTOs locales del controlador ────────────────────────────────────
    public class CrearEmpresaDTO
    {
        public string  Nombre { get; set; } = string.Empty;
        public string? Rfc    { get; set; }
    }

    public class CrearAdminEmpresaDTO
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email          { get; set; } = string.Empty;
        public string Password       { get; set; } = string.Empty;
    }
}