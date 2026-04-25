using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QualityDocAPI.DTOs;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "SoloAdmin")]  // Los logs son exclusivos del Admin
    public class LogsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LogsController(IConfiguration config)
        {
            _config = config;
        }

        // GET: api/Logs
        // Query params opcionales: idUsuario, accion, desde, hasta, pagina, tamano
        // Ejemplo: GET /api/Logs?accion=SUBIO&desde=2025-01-01&pagina=1&tamano=20
        [HttpGet]
        public IActionResult ObtenerLogs(
            [FromQuery] int?      idUsuario = null,
            [FromQuery] string?   accion    = null,
            [FromQuery] DateTime? desde     = null,
            [FromQuery] DateTime? hasta     = null,
            [FromQuery] int       pagina    = 1,
            [FromQuery] int       tamano    = 50)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1 || tamano > 200) tamano = 50;

            try
            {
                var condiciones = new List<string> { "1=1" };
                var parametros  = new List<SqlParameter>();

                if (idUsuario.HasValue)
                {
                    condiciones.Add("l.id_usuario = @idUsuario");
                    parametros.Add(new SqlParameter("@idUsuario", idUsuario.Value));
                }
                if (!string.IsNullOrWhiteSpace(accion))
                {
                    condiciones.Add("l.accion LIKE @accion");
                    parametros.Add(new SqlParameter("@accion", $"%{accion.ToUpper()}%"));
                }
                if (desde.HasValue)
                {
                    condiciones.Add("l.fecha_accion >= @desde");
                    parametros.Add(new SqlParameter("@desde", desde.Value));
                }
                if (hasta.HasValue)
                {
                    condiciones.Add("l.fecha_accion < @hasta");
                    parametros.Add(new SqlParameter("@hasta", hasta.Value.AddDays(1)));
                }

                string where  = string.Join(" AND ", condiciones);
                int    offset = (pagina - 1) * tamano;

                string sqlLogs = $@"
                    SELECT
                        l.id_log,
                        l.id_usuario,
                        u.nombre_completo  AS nombre_usuario,
                        ISNULL(ar.nombre, 'Sin área') AS area_usuario,
                        l.id_documento,
                        d.titulo           AS titulo_documento,
                        l.accion,
                        l.detalle,
                        l.ip_address,
                        l.fecha_accion
                    FROM  LogAccesos  l
                    INNER JOIN Usuarios   u  ON l.id_usuario   = u.id_usuario
                    LEFT  JOIN Areas      ar ON u.id_area       = ar.id_area
                    LEFT  JOIN Documentos d  ON l.id_documento  = d.id_documento
                    WHERE {where}
                    ORDER BY l.fecha_accion DESC
                    OFFSET {offset} ROWS FETCH NEXT {tamano} ROWS ONLY";

                string sqlCount = $@"
                    SELECT COUNT(*)
                    FROM  LogAccesos  l
                    INNER JOIN Usuarios u ON l.id_usuario = u.id_usuario
                    WHERE {where}";

                var  logs  = new List<LogDTO>();
                int  total = 0;

                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();

                    using (var cmdCount = new SqlCommand(sqlCount, con))
                    {
                        foreach (var p in parametros)
                            cmdCount.Parameters.AddWithValue(p.ParameterName, p.Value);
                        total = (int)cmdCount.ExecuteScalar();
                    }

                    using (var cmdLogs = new SqlCommand(sqlLogs, con))
                    {
                        foreach (var p in parametros)
                            cmdLogs.Parameters.AddWithValue(p.ParameterName, p.Value);

                        using (var rd = cmdLogs.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                logs.Add(new LogDTO
                                {
                                    IdLog           = (int)rd["id_log"],
                                    IdUsuario       = (int)rd["id_usuario"],
                                    NombreUsuario   = rd["nombre_usuario"].ToString()!,
                                    AreaUsuario     = rd["area_usuario"].ToString()!,
                                    IdDocumento     = rd["id_documento"]     == DBNull.Value ? null : (int?)rd["id_documento"],
                                    TituloDocumento = rd["titulo_documento"] == DBNull.Value ? null : rd["titulo_documento"].ToString(),
                                    Accion          = rd["accion"].ToString()!,
                                    Detalle         = rd["detalle"]    == DBNull.Value ? null : rd["detalle"].ToString(),
                                    IpAddress       = rd["ip_address"] == DBNull.Value ? null : rd["ip_address"].ToString(),
                                    FechaAccion     = (DateTime)rd["fecha_accion"]
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    TotalRegistros = total,
                    PaginaActual   = pagina,
                    TamanoPagina   = tamano,
                    TotalPaginas   = (int)Math.Ceiling(total / (double)tamano),
                    Logs           = logs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al consultar los logs.", Detalle = ex.Message });
            }
        }

        // GET: api/Logs/resumen
        // Dashboard del Admin: conteo de acciones en los últimos 30 días
        [HttpGet("resumen")]
        public IActionResult ObtenerResumen()
        {
            try
            {
                var resumen = new List<object>();
                int total   = 0;

                string sql = @"
                    SELECT accion, COUNT(*) AS cantidad
                    FROM   LogAccesos
                    WHERE  fecha_accion >= DATEADD(DAY, -30, GETDATE())
                    GROUP  BY accion
                    ORDER  BY cantidad DESC";

                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var rd  = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            int cantidad = (int)rd["cantidad"];
                            total += cantidad;
                            resumen.Add(new { Accion = rd["accion"].ToString(), Cantidad = cantidad });
                        }
                    }
                }

                return Ok(new
                {
                    Periodo          = $"Últimos 30 días hasta {DateTime.Now:dd/MM/yyyy}",
                    TotalMovimientos = total,
                    PorAccion        = resumen
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener el resumen.", Detalle = ex.Message });
            }
        }
    }
}