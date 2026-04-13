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

        // 🔒 Solo alguien con IdRol = 1 (Admin) puede crear usuarios
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
                    string query = @"INSERT INTO Usuarios (id_rol, nombre_completo, email, password_hash) 
                                    VALUES (@idRol, @nombre, @email, @pass)";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@idRol", datos.IdRol);
                        cmd.Parameters.AddWithValue("@nombre", datos.NombreCompleto);
                        cmd.Parameters.AddWithValue("@email", datos.Email);
                        cmd.Parameters.AddWithValue("@pass", passwordEncriptada);

                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { mensaje = "Usuario creado exitosamente por el administrador." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = "Error al crear usuario: " + ex.Message });
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDTO datos)
        {
            try
            {
                UsuarioSQL usuarioEncontrado = null;

                using (SqlConnection con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    string query = "SELECT id_usuario, id_rol, nombre_completo, password_hash FROM Usuarios WHERE email = @email AND activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@email", datos.Email);
                        using (SqlDataReader rd = cmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                usuarioEncontrado = new UsuarioSQL
                                {
                                    IdUsuario = (int)rd["id_usuario"],
                                    IdRol = (int)rd["id_rol"],
                                    NombreCompleto = rd["nombre_completo"].ToString(),
                                    PasswordHash = rd["password_hash"].ToString()
                                };
                            }
                        }
                    }
                }

                if (usuarioEncontrado == null || !BCrypt.Net.BCrypt.Verify(datos.Password, usuarioEncontrado.PasswordHash))
                {
                    return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
                }

                string tokenGenerado = GenerarTokenJWT(usuarioEncontrado);

                return Ok(new
                {
                    mensaje = "¡Bienvenido al sistema!",
                    usuario = usuarioEncontrado.NombreCompleto,
                    rol = usuarioEncontrado.IdRol,
                    token = tokenGenerado
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = "Error en el login: " + ex.Message });
            }
        }

        private string GenerarTokenJWT(UsuarioSQL usuario)
        {
            // ¡AQUÍ ESTÁ LA MAGIA CORREGIDA!
            var claims = new[]
            {
                // 1. Guardamos el ID con el nombre exacto que busca DocumentosController
                new Claim("id", usuario.IdUsuario.ToString()),
                // 2. Y también como el estándar de .NET (por si acaso)
                new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                
                new Claim(ClaimTypes.Name, usuario.NombreCompleto),
                
                // 3. Guardamos el Rol con el nombre exacto ("idRol")
                new Claim("idRol", usuario.IdRol.ToString()), 
                // 4. Y también como el estándar de roles de .NET (vital para [Authorize(Policy)])
                new Claim(ClaimTypes.Role, usuario.IdRol.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}