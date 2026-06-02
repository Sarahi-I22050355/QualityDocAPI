using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Data;
using QualityDocAPI.Models;
using Microsoft.Data.SqlClient;
using BCrypt.Net;
using QualityDocAPI.DTOs;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "EsSuperAdmin")]
    public class EmpresasController : ControllerBase
    {
        private readonly SqlContext     _sqlContext;
        private readonly IConfiguration _config;

        public EmpresasController(SqlContext sqlContext, IConfiguration config)
        {
            _sqlContext = sqlContext;
            _config     = config;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Empresas
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
        // Crea el área General y el admin en una sola transacción ADO.NET.
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("{idEmpresa}/admin")]
        public async Task<IActionResult> CrearAdminEmpresa(int idEmpresa, [FromBody] CrearAdminEmpresaDTO datos)
        {
            try
            {
                // Verificar que la empresa existe y está activa
                var empresa = await _sqlContext.Empresas.FindAsync(idEmpresa);
                if (empresa == null || !empresa.Activo)
                    return NotFound(new { Mensaje = "Empresa no encontrada o inactiva." });

                string hash = BCrypt.Net.BCrypt.HashPassword(datos.Password);

                using var con = new SqlConnection(_config.GetConnectionString("SqlConexion"));
                await con.OpenAsync();
                using var tx = con.BeginTransaction();

                try
                {
                    // 1. Verificar email duplicado
                    using (var cmdEmail = new SqlCommand(
                        "SELECT COUNT(*) FROM Usuarios WHERE email = @email", con, tx))
                    {
                        cmdEmail.Parameters.AddWithValue("@email", datos.Email);
                        int count = (int)await cmdEmail.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            tx.Rollback();
                            return BadRequest(new { Mensaje = $"El email '{datos.Email}' ya está registrado." });
                        }
                    }

                    // 2. Verificar si ya existe área General para esta empresa
                    int idAreaGeneral = 0;
                    using (var cmdArea = new SqlCommand(
                        "SELECT id_area FROM Areas WHERE id_empresa = @idEmpresa AND es_general = 1 AND activo = 1",
                        con, tx))
                    {
                        cmdArea.Parameters.AddWithValue("@idEmpresa", idEmpresa);
                        var resultado = await cmdArea.ExecuteScalarAsync();
                        if (resultado != null)
                            idAreaGeneral = (int)resultado;
                    }

                    // 3. Si no existe, crear el área General dentro de la misma transacción
                    if (idAreaGeneral == 0)
                    {
                        using var cmdCrearArea = new SqlCommand(@"
                            INSERT INTO Areas (nombre, descripcion, es_general, activo, id_empresa)
                            OUTPUT INSERTED.id_area
                            VALUES ('General', 'Área general de la empresa — visible para todos', 1, 1, @idEmpresa)",
                            con, tx);
                        cmdCrearArea.Parameters.AddWithValue("@idEmpresa", idEmpresa);
                        idAreaGeneral = (int)(await cmdCrearArea.ExecuteScalarAsync())!;
                    }

                    // 4. Insertar el admin usando el id_area recién obtenido
                    using (var cmdUsuario = new SqlCommand(@"
                        INSERT INTO Usuarios (id_rol, id_area, id_empresa, nombre_completo, email, password_hash)
                        VALUES (1, @idArea, @idEmpresa, @nombre, @email, @pass)",
                        con, tx))
                    {
                        cmdUsuario.Parameters.AddWithValue("@idArea",    idAreaGeneral);
                        cmdUsuario.Parameters.AddWithValue("@idEmpresa", idEmpresa);
                        cmdUsuario.Parameters.AddWithValue("@nombre",    datos.NombreCompleto);
                        cmdUsuario.Parameters.AddWithValue("@email",     datos.Email);
                        cmdUsuario.Parameters.AddWithValue("@pass",      hash);
                        await cmdUsuario.ExecuteNonQueryAsync();
                    }

                    tx.Commit();

                    return Ok(new
                    {
                        Mensaje  = $"Admin creado para la empresa '{empresa.Nombre}'.",
                        Email    = datos.Email,
                        Empresa  = empresa.Nombre
                    });
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al crear admin.", Detalle = ex.Message });
            }
        }
    }
}