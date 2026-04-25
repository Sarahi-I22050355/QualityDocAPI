using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QualityDocAPI.DTOs;
using QualityDocAPI.Models;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace QualityDocAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IConfiguration _config;

        public UsuariosController(IConfiguration config)
        {
            _config = config;
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Usuarios
        // Lista todos los usuarios del sistema con su rol y área.
        // Solo Admin. Útil para el panel de gestión de usuarios en el frontend.
        // ─────────────────────────────────────────────────────────────────
        [Authorize(Policy = "SoloAdmin")]
        [HttpGet]
        public IActionResult ObtenerUsuarios()
        {
            try
            {
                var usuarios = new List<object>();

                string query = @"
                    SELECT
                        u.id_usuario,
                        u.nombre_completo,
                        u.email,
                        u.activo,
                        u.fecha_creacion,
                        u.ultimo_acceso,
                        r.nombre_rol,
                        ISNULL(a.nombre, 'Sin área') AS nombre_area
                    FROM  Usuarios u
                    INNER JOIN Roles r ON u.id_rol  = r.id_rol
                    LEFT  JOIN Areas a ON u.id_area = a.id_area
                    ORDER BY u.fecha_creacion DESC";

                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using var cmd = new SqlCommand(query, con);
                    using var rd  = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        usuarios.Add(new
                        {
                            IdUsuario      = (int)rd["id_usuario"],
                            NombreCompleto = rd["nombre_completo"].ToString(),
                            Email          = rd["email"].ToString(),
                            Activo         = (bool)rd["activo"],
                            Rol            = rd["nombre_rol"].ToString(),
                            Area           = rd["nombre_area"].ToString(),
                            FechaCreacion  = (DateTime)rd["fecha_creacion"],
                            UltimoAcceso   = rd["ultimo_acceso"] == DBNull.Value
                                                ? (DateTime?)null
                                                : (DateTime)rd["ultimo_acceso"]
                        });
                    }
                }

                return Ok(new { Total = usuarios.Count, Usuarios = usuarios });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al obtener usuarios.", detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT: api/Usuarios/{id}/estado
        // Activa o desactiva un usuario.
        // Solo Admin. El Admin no puede desactivarse a sí mismo.
        // Body: { "Activo": false }
        // ─────────────────────────────────────────────────────────────────
        [Authorize(Policy = "SoloAdmin")]
        [HttpPut("{id}/estado")]
        public IActionResult CambiarEstadoUsuario(int id, [FromBody] CambiarEstadoUsuarioDTO datos)
        {
            try
            {
                // El Admin no puede desactivarse a sí mismo
                var idAdminActual = User.FindFirst("id")?.Value;
                if (idAdminActual == id.ToString() && !datos.Activo)
                    return BadRequest(new { mensaje = "No puedes desactivarte a ti mismo." });

                string query = @"
                    UPDATE Usuarios
                    SET    activo = @activo
                    WHERE  id_usuario = @id";

                int filasAfectadas;
                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using var cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@activo", datos.Activo);
                    cmd.Parameters.AddWithValue("@id",     id);
                    filasAfectadas = cmd.ExecuteNonQuery();
                }

                if (filasAfectadas == 0)
                    return NotFound(new { mensaje = $"No existe un usuario con ID {id}." });

                string accion = datos.Activo ? "activado" : "desactivado";
                return Ok(new { mensaje = $"Usuario {accion} correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al cambiar estado.", detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST: api/Usuarios/crear
        // Solo Admin puede crear usuarios y asignarles área.
        // ─────────────────────────────────────────────────────────────────
        [Authorize(Policy = "SoloAdmin")]
        [HttpPost("crear")]
        public IActionResult CrearUsuario([FromBody] CrearUsuarioDTO datos)
        {
            try
            {
                string passwordEncriptada = BCrypt.Net.BCrypt.HashPassword(datos.Password);

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();

                    string verificarArea = "SELECT COUNT(*) FROM Areas WHERE id_area = @idArea AND activo = 1";
                    using (SqlCommand cmdCheck = new SqlCommand(verificarArea, con))
                    {
                        cmdCheck.Parameters.AddWithValue("@idArea", datos.IdArea);
                        int existe = (int)cmdCheck.ExecuteScalar();
                        if (existe == 0)
                            return BadRequest(new { mensaje = $"El área con ID {datos.IdArea} no existe o está inactiva." });
                    }

                    string query = @"INSERT INTO Usuarios (id_rol, id_area, nombre_completo, email, password_hash)
                                     VALUES (@idRol, @idArea, @nombre, @email, @pass)";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@idRol",  datos.IdRol);
                        cmd.Parameters.AddWithValue("@idArea", datos.IdArea);
                        cmd.Parameters.AddWithValue("@nombre", datos.NombreCompleto);
                        cmd.Parameters.AddWithValue("@email",  datos.Email);
                        cmd.Parameters.AddWithValue("@pass",   passwordEncriptada);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { mensaje = "Usuario creado exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = "Error al crear usuario: " + ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // POST: api/Usuarios/login
        // ─────────────────────────────────────────────────────────────────
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDTO datos)
        {
            try
            {
                UsuarioSQL? usuarioEncontrado = null;

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();

                    string query = @"
                        SELECT
                            u.id_usuario,
                            u.id_rol,
                            u.nombre_completo,
                            u.password_hash,
                            u.id_area,
                            ISNULL(a.es_general, 0)      AS es_area_general,
                            ISNULL(a.nombre, 'Sin Área') AS nombre_area
                        FROM Usuarios u
                        LEFT JOIN Areas a ON u.id_area = a.id_area
                        WHERE u.email = @email AND u.activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@email", datos.Email);
                        using (SqlDataReader rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                usuarioEncontrado = new UsuarioSQL
                                {
                                    IdUsuario      = (int)rd["id_usuario"],
                                    IdRol          = (int)rd["id_rol"],
                                    NombreCompleto = rd["nombre_completo"].ToString()!,
                                    PasswordHash   = rd["password_hash"].ToString()!,
                                    IdArea         = rd["id_area"] == DBNull.Value ? null : (int?)rd["id_area"],
                                    EsAreaGeneral  = (bool)rd["es_area_general"],
                                    NombreArea     = rd["nombre_area"].ToString()!
                                };
                            }
                        }
                    }
                }

                if (usuarioEncontrado == null ||
                    !BCrypt.Net.BCrypt.Verify(datos.Password, usuarioEncontrado.PasswordHash))
                {
                    return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
                }

                string tokenGenerado = GenerarTokenJWT(usuarioEncontrado);

                return Ok(new
                {
                    mensaje   = "¡Bienvenido al sistema!",
                    usuario   = usuarioEncontrado.NombreCompleto,
                    rol       = usuarioEncontrado.IdRol,
                    area      = usuarioEncontrado.NombreArea,
                    esGeneral = usuarioEncontrado.EsAreaGeneral,
                    token     = tokenGenerado
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = "Error en el login: " + ex.Message });
            }
        }

        private string GenerarTokenJWT(UsuarioSQL usuario)
        {
            var claims = new[]
            {
                new Claim("id",                      usuario.IdUsuario.ToString()),
                new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                new Claim(ClaimTypes.Name,           usuario.NombreCompleto),
                new Claim("idRol",                   usuario.IdRol.ToString()),
                new Claim(ClaimTypes.Role,           usuario.IdRol.ToString()),
                new Claim("id_area",                 usuario.IdArea?.ToString() ?? "0"),
                new Claim("es_area_general",         usuario.EsAreaGeneral.ToString().ToLower()),
                new Claim("nombre_area",             usuario.NombreArea)
            };

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}