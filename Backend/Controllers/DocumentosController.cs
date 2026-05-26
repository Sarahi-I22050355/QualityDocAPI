using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Models;
using QualityDocAPI.DTOs;
using QualityDocAPI.Data;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Linq;
using UglyToad.PdfPig;
using System.IO;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using iText.Html2pdf;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentosController : ControllerBase
    {
        private readonly SqlContext _sqlContext;
        private readonly IMongoCollection<DocumentoMongo> _mongoCollection;
        private readonly IConfiguration _config;

        public DocumentosController(SqlContext sqlContext, IMongoClient mongoClient, IConfiguration config)
        {
            _sqlContext = sqlContext;
            _config = config;
            var database = mongoClient.GetDatabase("QualityDocPolyglotDB");
            _mongoCollection = database.GetCollection<DocumentoMongo>("documentosBusqueda");
        }

        // ─────────────────────────────────────────────────────────────────
        // MÉTODO AUXILIAR: extrae los claims de identidad del token JWT.
        // ─────────────────────────────────────────────────────────────────
        private (int idUsuario, string rol, bool esGeneral, int? idArea) ObtenerDatosToken()
        {
            var idStr   = User.FindFirst("id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var rol     = User.FindFirst("idRol")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var esg     = User.FindFirst("es_area_general")?.Value == "true";
            var areaStr = User.FindFirst("id_area")?.Value;
            int? area   = int.TryParse(areaStr, out int a) && a != 0 ? a : (int?)null;
            int  id     = int.TryParse(idStr, out int u) ? u : 0;
            return (id, rol, esg, area);
        }

        // ─────────────────────────────────────────────────────────────────
        // MÉTODO AUXILIAR: inserta un registro en LogAccesos.
        // ─────────────────────────────────────────────────────────────────
        private async Task RegistrarLogAsync(int idUsuario, int? idDocumento, string accion, string? detalle)
        {
            try
            {
                _sqlContext.LogAccesos.Add(new LogAccesoSQL
                {
                    IdUsuario   = idUsuario,
                    IdDocumento = idDocumento,
                    Accion      = accion,
                    Detalle     = detalle,
                    IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    FechaAccion = DateTime.Now
                });
                await _sqlContext.SaveChangesAsync();
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCESO PARA VER / DESCARGAR (lectura amplia)
        // ─────────────────────────────────────────────────────────────────
        private bool TieneAccesoParaVer(string rol, bool esGeneral, int? idAreaToken, int? idAreaDocumento)
        {
            if (rol == "1" || esGeneral) return true;
            if (idAreaDocumento == idAreaToken) return true;

            if (idAreaDocumento.HasValue)
            {
                var areaDelDocumento = _sqlContext.Areas.Find(idAreaDocumento.Value);
                if (areaDelDocumento != null && areaDelDocumento.EsGeneral && areaDelDocumento.Activo)
                    return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCESO PARA MODIFICAR (escritura/aprobación)
        // Rol 4 (Revisor) puede resolver aprobaciones en su área.
        // ─────────────────────────────────────────────────────────────────
        private bool TieneAccesoParaModificar(string rol, bool esGeneral, int? idAreaToken, int? idAreaDocumento)
        {
            if (rol == "1" || esGeneral) return true;
            return idAreaDocumento == idAreaToken;
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST: api/Documentos
        // Sube un documento nuevo (versión 1).
        // Roles: Admin (1) y Supervisor (2).
        // El Autor se toma del token JWT.
        // El Área se hereda del token SALVO que el usuario sea de área General,
        // en cuyo caso puede elegir cualquier área.
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPost]
        public async Task<IActionResult> SubirDocumento([FromForm] NuevoDocumentoDTO datosDePantalla)
        {
            if (datosDePantalla == null)
                return BadRequest(new { Mensaje = "No se recibieron datos en el formulario." });

            bool tieneArchivo = datosDePantalla.Archivo != null && datosDePantalla.Archivo.Length > 0;
            bool tieneTexto   = !string.IsNullOrWhiteSpace(datosDePantalla.ContenidoTexto);

            if (!tieneArchivo && !tieneTexto)
                return BadRequest(new { Mensaje = "Debes escribir el contenido o adjuntar un archivo PDF." });

            var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();
            var nombreArea = User.FindFirst("nombre_area")?.Value ?? "General";

            // Autor siempre viene del token
            var nombreUsuario = User.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario del sistema";

            int?   idAreaDocumento;
            string nombreAreaDocumento;

            // Solo usuarios de área General pueden elegir otra área
            if (esGeneral && datosDePantalla.IdArea.HasValue && datosDePantalla.IdArea.Value > 0)
            {
                var areaElegida = await _sqlContext.Areas.FindAsync(datosDePantalla.IdArea.Value);
                if (areaElegida == null || !areaElegida.Activo)
                    return BadRequest(new { Mensaje = $"El área con ID {datosDePantalla.IdArea} no existe." });
                idAreaDocumento     = areaElegida.Id;
                nombreAreaDocumento = areaElegida.Nombre;
            }
            else
            {
                // Todos los demás heredan su área del token
                idAreaDocumento     = idAreaToken;
                nombreAreaDocumento = nombreArea;
            }

            string textoExtraido      = "";
            string rutaFinalEnLinux   = "Sin archivo físico";
            string nombreArchivoFinal = "N/A";
            long   tamanoBytes        = 0;

            try
            {
                if (tieneArchivo)
                {
                    tamanoBytes = datosDePantalla.Archivo!.Length;

                    using (var stream = datosDePantalla.Archivo.OpenReadStream())
                    using (var pdf = PdfDocument.Open(stream))
                        foreach (var page in pdf.GetPages())
                            textoExtraido += page.Text + " ";

                    var sftpSettings   = _config.GetSection("SftpConfig");
                    nombreArchivoFinal = $"{Guid.NewGuid()}_{datosDePantalla.Archivo.FileName}";
                    string remotePath  = sftpSettings["RemotePath"] ?? "/uploads";
                    rutaFinalEnLinux   = $"{remotePath}/{nombreArchivoFinal}";

                    using (var client = new SftpClient(
                        sftpSettings["Host"]     ?? "localhost",
                        int.TryParse(sftpSettings["Port"], out int p) ? p : 2222,
                        sftpSettings["Username"] ?? "sarahi",
                        sftpSettings["Password"] ?? "12345"))
                    {
                        client.Connect();
                        using (var us = datosDePantalla.Archivo.OpenReadStream())
                            client.UploadFile(us, rutaFinalEnLinux);
                        client.Disconnect();
                    }
                }
                else
                {
                    textoExtraido = datosDePantalla.ContenidoTexto!;
                    tamanoBytes   = System.Text.Encoding.UTF8.GetByteCount(textoExtraido);
                }

                string nombreCategoriaMongo = datosDePantalla.IdCategoria switch
                {
                    1 => "Manual de Calidad",
                    2 => "Procedimiento",
                    3 => "Instrucción de Trabajo",
                    4 => "Registro de Calidad",
                    5 => "Plan de Control",
                    6 => "Auditoría",
                    _ => "Categoría General"
                };

                var documentoSQL = new DocumentoSQL
                {
                    Titulo        = datosDePantalla.Titulo,
                    Descripcion   = tieneArchivo ? "Documento cargado vía PDF" : datosDePantalla.ContenidoTexto!,
                    IdUsuario     = idUsuario,
                    IdCategoria   = datosDePantalla.IdCategoria,
                    IdEstado      = 1,
                    IdArea        = idAreaDocumento,
                    RutaArchivo   = rutaFinalEnLinux,
                    NombreArchivo = nombreArchivoFinal,
                    Extension     = tieneArchivo ? "pdf" : "txt",
                    NumeroVersion = 1
                };
                _sqlContext.Documentos.Add(documentoSQL);
                await _sqlContext.SaveChangesAsync();

                _sqlContext.VersionesDocumento.Add(new VersionDocumentoSQL
                {
                    IdDocumento      = documentoSQL.Id,
                    IdUsuario        = idUsuario,
                    NumeroVersion    = 1,
                    RutaArchivo      = rutaFinalEnLinux,
                    NombreArchivo    = nombreArchivoFinal,
                    TamanoBytes      = tamanoBytes,
                    ComentarioCambio = "Versión inicial del documento.",
                    FechaVersion     = DateTime.Now
                });
                await _sqlContext.SaveChangesAsync();

                await RegistrarLogAsync(idUsuario, documentoSQL.Id, "SUBIO", nombreArchivoFinal);

                // Guardar en MongoDB con auditoría completa
                await _mongoCollection.InsertOneAsync(new DocumentoMongo
                {
                    SqlId      = documentoSQL.Id,
                    Titulo     = datosDePantalla.Titulo,
                    Descripcion = textoExtraido,
                    Categoria  = nombreCategoriaMongo,
                    Autor      = nombreUsuario,
                    Extension  = tieneArchivo ? "pdf" : "txt",
                    Area       = nombreAreaDocumento,
                    Etiquetas  = datosDePantalla.Etiquetas?
                                    .Where(e => !string.IsNullOrWhiteSpace(e))
                                    .Select(e => e.ToLower())
                                    .ToArray() ?? Array.Empty<string>(),
                    SubidoPor  = nombreUsuario,
                    FechaSubida = DateTime.Now,
                    UltimoFlujo = null
                });

                return Ok(new
                {
                    Mensaje         = tieneArchivo ? "¡PDF subido y procesado!" : "¡Texto guardado correctamente!",
                    IdGeneradoEnSQL = documentoSQL.Id,
                    Version         = 1,
                    Area            = nombreAreaDocumento,
                    Ubicacion       = rutaFinalEnLinux,
                    SiguientePaso   = $"Solicita su aprobación en: PUT /api/Documentos/solicitar-aprobacion/{documentoSQL.Id}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al procesar el documento.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/buscar/{palabraClave}
        // Busca en MongoDB — los datos de auditoría ya vienen dentro del doc.
        // ═══════════════════════════════════════════════════════════════════
        [Authorize]
        [HttpGet("buscar/{palabraClave}")]
        public async Task<IActionResult> BuscarDocumentos(string palabraClave)
        {
            try
            {
                var (_, rol, esGeneral, _) = ObtenerDatosToken();
                var nombreArea = User.FindFirst("nombre_area")?.Value ?? "";
                var busqueda   = palabraClave.ToLower();
                var builder    = Builders<DocumentoMongo>.Filter;

                var filtroTexto = builder.Or(
                    builder.AnyEq(d => d.Etiquetas,  busqueda),
                    builder.Regex(d => d.Titulo,      new MongoDB.Bson.BsonRegularExpression(busqueda, "i")),
                    builder.Regex(d => d.Descripcion, new MongoDB.Bson.BsonRegularExpression(busqueda, "i")),
                    builder.Regex(d => d.Autor,       new MongoDB.Bson.BsonRegularExpression(busqueda, "i"))
                );

                FilterDefinition<DocumentoMongo> filtroFinal;

                if (esGeneral || rol == "1")
                {
                    filtroFinal = filtroTexto;
                }
                else
                {
                    var nombresAreasGenerales = _sqlContext.Areas
                        .Where(a => a.EsGeneral && a.Activo)
                        .Select(a => a.Nombre)
                        .ToList();

                    var filtroArea = builder.Or(
                        builder.Eq(d => d.Area, nombreArea),
                        builder.In(d => d.Area, nombresAreasGenerales)
                    );

                    filtroFinal = builder.And(filtroTexto, filtroArea);
                }

                var resultadosMongo = await _mongoCollection.Find(filtroFinal).ToListAsync();
                if (resultadosMongo.Count == 0)
                    return NotFound(new { Mensaje = $"No hay resultados para: '{palabraClave}'" });

                var idsMongo      = resultadosMongo.Select(r => r.SqlId).ToList();
                var documentosSQL = _sqlContext.Documentos.Where(d => idsMongo.Contains(d.Id)).ToList();

                if (rol == "3") // Operario: solo Aprobados
                {
                    var idsAprobados = documentosSQL.Where(d => d.IdEstado == 2).Select(d => d.Id).ToList();
                    var finales      = resultadosMongo.Where(m => idsAprobados.Contains(m.SqlId)).ToList();
                    if (finales.Count == 0)
                        return NotFound(new { Mensaje = "No hay documentos aprobados que coincidan con tu búsqueda." });
                    return Ok(new { Mensaje = "Búsqueda exitosa", Resultados = finales });
                }

                var resultadosConEstado = resultadosMongo.Select(docMongo =>
                {
                    var docSql = documentosSQL.FirstOrDefault(d => d.Id == docMongo.SqlId);
                    return new
                    {
                        Documento = docMongo,
                        Estado    = docSql?.IdEstado switch { 1 => "Borrador", 2 => "Aprobado", 3 => "Obsoleto", _ => "Desconocido" },
                        Version   = docSql?.NumeroVersion
                    };
                }).ToList();

                return Ok(new { Mensaje = "Búsqueda exitosa", Resultados = resultadosConEstado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error de búsqueda", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/pendientes-revision
        // Para el Revisor: muestra todos los documentos con solicitud pendiente
        // filtrados por su área (o todos si es área General).
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "PuedeRevisar")]
        [HttpGet("pendientes-revision")]
        public async Task<IActionResult> ObtenerPendientesRevision()
        {
            try
            {
                var (_, rol, esGeneral, idAreaToken) = ObtenerDatosToken();
                var nombreArea = User.FindFirst("nombre_area")?.Value ?? "";
                var builder    = Builders<DocumentoMongo>.Filter;

                // Filtrar por área según el rol del revisor
                FilterDefinition<DocumentoMongo> filtroArea;
                if (esGeneral || rol == "1")
                {
                    filtroArea = builder.Empty;
                }
                else
                {
                    var nombresAreasGenerales = _sqlContext.Areas
                        .Where(a => a.EsGeneral && a.Activo)
                        .Select(a => a.Nombre)
                        .ToList();

                    filtroArea = builder.Or(
                        builder.Eq(d => d.Area, nombreArea),
                        builder.In(d => d.Area, nombresAreasGenerales)
                    );
                }

                var todosMongo = await _mongoCollection.Find(filtroArea).ToListAsync();
                var idsMongo   = todosMongo.Select(r => r.SqlId).ToList();

                // Obtener solo los que tienen solicitud pendiente
                var idsPendientes = _sqlContext.FlujoAprobacion
                    .Where(f => f.Decision == "Pendiente" && idsMongo.Contains(f.IdDocumento))
                    .Select(f => f.IdDocumento)
                    .Distinct()
                    .ToList();

                if (idsPendientes.Count == 0)
                    return Ok(new { Mensaje = "No hay documentos pendientes de revisión.", Total = 0, Resultados = new List<object>() });

                var documentosSQL = _sqlContext.Documentos
                    .Where(d => idsPendientes.Contains(d.Id))
                    .ToList();

                var resultados = documentosSQL.Select(docSql =>
                {
                    var docMongo = todosMongo.FirstOrDefault(m => m.SqlId == docSql.Id);
                    return new
                    {
                        Documento = docMongo,
                        Estado    = docSql.IdEstado switch { 1 => "Borrador", 2 => "Aprobado", 3 => "Obsoleto", _ => "Desconocido" },
                        Version   = docSql.NumeroVersion
                    };
                }).ToList();

                return Ok(new { Mensaje = "Documentos pendientes de revisión", Total = resultados.Count, Resultados = resultados });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener pendientes.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PUT: api/Documentos/solicitar-aprobacion/{id}
        // Solo Admin y Supervisor pueden solicitar revisión.
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPut("solicitar-aprobacion/{id}")]
        public async Task<IActionResult> SolicitarAprobacion(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });
                if (documento.IdEstado != 1)
                    return BadRequest(new { Mensaje = "Solo se pueden enviar a revisión documentos en estado Borrador." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes solicitar aprobación para documentos de otra área." });

                bool yaExistePendiente = _sqlContext.FlujoAprobacion
                    .Any(f => f.IdDocumento == id && f.Decision == "Pendiente");
                if (yaExistePendiente)
                    return BadRequest(new { Mensaje = "Este documento ya tiene una solicitud de aprobación pendiente." });

                var flujo = new FlujoAprobacionSQL
                {
                    IdDocumento    = id,
                    IdSolicitante  = idUsuario,
                    Decision       = "Pendiente",
                    FechaSolicitud = DateTime.Now
                };
                _sqlContext.FlujoAprobacion.Add(flujo);
                await _sqlContext.SaveChangesAsync();

                // Actualizar MongoDB para reflejar que está pendiente
                var filtroMongo = Builders<DocumentoMongo>.Filter.Eq(d => d.SqlId, id);
                var updateMongo = Builders<DocumentoMongo>.Update.Set(d => d.UltimoFlujo, new UltimoFlujoMongo
                {
                    Decision        = "Pendiente",
                    RevisadoPor     = "",
                    FechaResolucion = DateTime.Now
                });
                await _mongoCollection.UpdateOneAsync(filtroMongo, updateMongo);

                await RegistrarLogAsync(idUsuario, id, "SOLICITÓ REVISIÓN", documento.Titulo);

                return Ok(new
                {
                    Mensaje         = "Solicitud de revisión creada correctamente.",
                    IdFlujo         = flujo.Id,
                    IdDocumento     = id,
                    TituloDocumento = documento.Titulo,
                    Version         = documento.NumeroVersion,
                    SiguientePaso   = $"El revisor debe llamar: PUT /api/Documentos/resolver-aprobacion/{flujo.Id}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al solicitar aprobación.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PUT: api/Documentos/resolver-aprobacion/{idFlujo}
        // Solo Admin (1) y Revisor (4) pueden resolver aprobaciones.
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "PuedeRevisar")]
        [HttpPut("resolver-aprobacion/{idFlujo}")]
        public async Task<IActionResult> ResolverAprobacion(int idFlujo, [FromBody] ResolverAprobacionDTO datos)
        {
            if (datos.Decision != "Aprobado" && datos.Decision != "Rechazado")
                return BadRequest(new { Mensaje = "La decisión debe ser exactamente 'Aprobado' o 'Rechazado'." });

            try
            {
                var flujo = await _sqlContext.FlujoAprobacion.FindAsync(idFlujo);
                if (flujo == null)
                    return NotFound(new { Mensaje = "Solicitud de aprobación no encontrada." });
                if (flujo.Decision != "Pendiente")
                    return BadRequest(new { Mensaje = $"Esta solicitud ya fue resuelta con decisión: '{flujo.Decision}'." });

                var documento = await _sqlContext.Documentos.FindAsync(flujo.IdDocumento);
                if (documento == null)
                    return NotFound(new { Mensaje = "El documento asociado no existe." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes resolver aprobaciones de documentos de otra área." });

                var nombreRevisor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Revisor";

                flujo.IdRevisor       = idUsuario;
                flujo.Decision        = datos.Decision;
                flujo.Comentarios     = datos.Comentarios;
                flujo.FechaResolucion = DateTime.Now;

                documento.IdEstado          = datos.Decision == "Aprobado" ? 2 : 1;
                documento.FechaModificacion = DateTime.Now;

                await _sqlContext.SaveChangesAsync();

                // Actualizar MongoDB con el resultado de la aprobación
                var filtroMongo = Builders<DocumentoMongo>.Filter.Eq(d => d.SqlId, documento.Id);
                var updateMongo = Builders<DocumentoMongo>.Update.Set(d => d.UltimoFlujo, new UltimoFlujoMongo
                {
                    Decision        = datos.Decision,
                    RevisadoPor     = nombreRevisor,
                    FechaResolucion = DateTime.Now
                });
                await _mongoCollection.UpdateOneAsync(filtroMongo, updateMongo);

                string accionLog = datos.Decision == "Aprobado" ? "APROBÓ" : "RECHAZÓ";
                await RegistrarLogAsync(idUsuario, documento.Id, accionLog, datos.Comentarios ?? documento.Titulo);

                string mensajeResultado = datos.Decision == "Aprobado"
                    ? "Documento aprobado. Ya está disponible para todos los usuarios del sistema."
                    : "Documento rechazado. Regresa a Borrador.";

                return Ok(new
                {
                    Mensaje     = mensajeResultado,
                    IdFlujo     = flujo.Id,
                    IdDocumento = documento.Id,
                    Decision    = flujo.Decision,
                    Comentarios = flujo.Comentarios,
                    NuevoEstado = datos.Decision == "Aprobado" ? "Aprobado" : "Borrador"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al resolver la aprobación.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/{id}/flujo
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpGet("{id}/flujo")]
        public async Task<IActionResult> ObtenerFlujoPorDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (_, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaVer(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes ver el flujo de documentos de otra área." });

                var historial = new List<object>();

                const string sql = @"
                    SELECT
                        f.id_flujo,
                        f.decision,
                        f.comentarios,
                        f.fecha_solicitud,
                        f.fecha_resolucion,
                        us.nombre_completo  AS nombre_solicitante,
                        ur.nombre_completo  AS nombre_revisor
                    FROM  FlujoAprobacion f
                    INNER JOIN Usuarios us ON f.id_solicitante = us.id_usuario
                    LEFT  JOIN Usuarios ur ON f.id_revisor     = ur.id_usuario
                    WHERE f.id_documento = @id
                    ORDER BY f.fecha_solicitud ASC";

                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using var cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", id);
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        historial.Add(new
                        {
                            IdFlujo           = (int)rd["id_flujo"],
                            Decision          = rd["decision"].ToString(),
                            Comentarios       = rd["comentarios"]      == DBNull.Value ? null : rd["comentarios"].ToString(),
                            NombreSolicitante = rd["nombre_solicitante"].ToString(),
                            NombreRevisor     = rd["nombre_revisor"]    == DBNull.Value
                                                    ? "Pendiente de revisión"
                                                    : rd["nombre_revisor"].ToString(),
                            FechaSolicitud    = (DateTime)rd["fecha_solicitud"],
                            FechaResolucion   = rd["fecha_resolucion"]  == DBNull.Value
                                                    ? (DateTime?)null
                                                    : (DateTime)rd["fecha_resolucion"]
                        });
                    }
                }

                bool hayPendiente = _sqlContext.FlujoAprobacion
                    .Any(f => f.IdDocumento == id && f.Decision == "Pendiente");

                return Ok(new
                {
                    IdDocumento        = id,
                    TituloDocumento    = documento.Titulo,
                    Version            = documento.NumeroVersion,
                    EstadoActual       = documento.IdEstado switch { 1 => "Borrador", 2 => "Aprobado", 3 => "Obsoleto", _ => "Desconocido" },
                    HaySolicitudActiva = hayPendiente,
                    TotalMovimientos   = historial.Count,
                    Historial          = historial
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener el flujo.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/descargar/{id}
        // ═══════════════════════════════════════════════════════════════════
        [Authorize]
        [HttpGet("descargar/{id}")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaVer(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "Este documento no pertenece a tu área." });

                if (rol == "3" && documento.IdEstado != 2)
                    return StatusCode(403, new { Mensaje = "Solo puedes descargar documentos aprobados." });

                var memoryStream = new MemoryStream();

                if (documento.RutaArchivo != "Sin archivo físico")
                {
                    var sftpSettings = _config.GetSection("SftpConfig");
                    using (var client = new SftpClient(
                        sftpSettings["Host"]     ?? "localhost",
                        int.TryParse(sftpSettings["Port"], out int p) ? p : 2222,
                        sftpSettings["Username"] ?? "sarahi",
                        sftpSettings["Password"] ?? "12345"))
                    {
                        client.Connect();
                        client.DownloadFile(documento.RutaArchivo, memoryStream);
                        client.Disconnect();
                    }
                    memoryStream.Position = 0;

                    string nombreBonito = documento.NombreArchivo;
                    int    idx          = nombreBonito.IndexOf('_');
                    nombreBonito = idx >= 0 ? nombreBonito.Substring(idx + 1) : $"{documento.Titulo}.pdf";

                    await RegistrarLogAsync(idUsuario, id, "DESCARGÓ", nombreBonito);
                    return File(memoryStream, "application/pdf", nombreBonito);
                }
                else
                {
                    string titulo    = string.IsNullOrWhiteSpace(documento.Titulo)      ? "Documento Sin Título"             : documento.Titulo;
                    string contenido = string.IsNullOrWhiteSpace(documento.Descripcion) ? "El documento no tiene contenido." : documento.Descripcion;

                    string html = $@"
                        <html><head><style>
                            body {{ font-family: 'Helvetica', sans-serif; padding: 30px; color: #333; }}
                            h1   {{ color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px; }}
                            .contenido {{ font-size: 12pt; line-height: 1.6; margin-top: 20px; }}
                            .footer {{ margin-top: 50px; font-size: 8pt; color: #7f8c8d; text-align: center; border-top: 1px solid #eee; padding-top: 10px; }}
                        </style></head><body>
                            <h1>{titulo}</h1>
                            <div class='contenido'>{contenido}</div>
                            <div class='footer'>Documento generado por QualityDoc System - {DateTime.Now:dd/MM/yyyy}</div>
                        </body></html>";

                    byte[] pdfBytes;
                    using (var ms = new MemoryStream())
                    {
                        HtmlConverter.ConvertToPdf(html, ms);
                        pdfBytes = ms.ToArray();
                    }

                    var nombre = string.Join("_", titulo.Split(Path.GetInvalidFileNameChars())) + ".pdf";
                    await RegistrarLogAsync(idUsuario, id, "DESCARGÓ", nombre);
                    return File(pdfBytes, "application/pdf", nombre);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error en la descarga", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST: api/Documentos/{id}/nueva-version
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPost("{id}/nueva-version")]
        public async Task<IActionResult> SubirNuevaVersion(int id, [FromForm] NuevaVersionDTO datos)
        {
            bool tieneArchivo = datos.Archivo != null && datos.Archivo.Length > 0;
            bool tieneTexto   = !string.IsNullOrWhiteSpace(datos.ContenidoTexto);

            if (!tieneArchivo && !tieneTexto)
                return BadRequest(new { Mensaje = "Debes adjuntar un archivo PDF o escribir el contenido." });

            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes subir versiones de documentos de otra área." });

                bool hayPendiente = _sqlContext.FlujoAprobacion
                    .Any(f => f.IdDocumento == id && f.Decision == "Pendiente");
                if (hayPendiente)
                    return BadRequest(new { Mensaje = "Hay una solicitud de aprobación pendiente. Resuélvela antes de subir una nueva versión." });

                string textoExtraido      = "";
                string rutaNueva          = documento.RutaArchivo;
                string nombreArchivoNuevo = documento.NombreArchivo;
                long   tamanoBytes        = 0;

                if (tieneArchivo)
                {
                    tamanoBytes = datos.Archivo!.Length;

                    using (var stream = datos.Archivo.OpenReadStream())
                    using (var pdf = PdfDocument.Open(stream))
                        foreach (var page in pdf.GetPages())
                            textoExtraido += page.Text + " ";

                    var sftpSettings   = _config.GetSection("SftpConfig");
                    nombreArchivoNuevo = $"{Guid.NewGuid()}_{datos.Archivo.FileName}";
                    string remotePath  = sftpSettings["RemotePath"] ?? "/uploads";
                    rutaNueva          = $"{remotePath}/{nombreArchivoNuevo}";

                    using (var client = new SftpClient(
                        sftpSettings["Host"]     ?? "localhost",
                        int.TryParse(sftpSettings["Port"], out int p) ? p : 2222,
                        sftpSettings["Username"] ?? "sarahi",
                        sftpSettings["Password"] ?? "12345"))
                    {
                        client.Connect();
                        using (var us = datos.Archivo.OpenReadStream())
                            client.UploadFile(us, rutaNueva);
                        client.Disconnect();
                    }
                }
                else
                {
                    textoExtraido = datos.ContenidoTexto!;
                    tamanoBytes   = System.Text.Encoding.UTF8.GetByteCount(textoExtraido);
                }

                short nuevaVersion = (short)(documento.NumeroVersion + 1);

                _sqlContext.VersionesDocumento.Add(new VersionDocumentoSQL
                {
                    IdDocumento      = id,
                    IdUsuario        = idUsuario,
                    NumeroVersion    = nuevaVersion,
                    RutaArchivo      = rutaNueva,
                    NombreArchivo    = nombreArchivoNuevo,
                    TamanoBytes      = tamanoBytes,
                    ComentarioCambio = datos.ComentarioCambio,
                    FechaVersion     = DateTime.Now
                });

                documento.NumeroVersion     = nuevaVersion;
                documento.RutaArchivo       = rutaNueva;
                documento.NombreArchivo     = nombreArchivoNuevo;
                documento.Descripcion       = tieneArchivo
                                                ? $"v{nuevaVersion} - Documento actualizado vía PDF"
                                                : datos.ContenidoTexto!;
                documento.IdEstado          = 1;
                documento.FechaModificacion = DateTime.Now;

                await _sqlContext.SaveChangesAsync();

                var filtroMongo = Builders<DocumentoMongo>.Filter.Eq(d => d.SqlId, id);
                var updateMongo = Builders<DocumentoMongo>.Update
                    .Set(d => d.Descripcion, textoExtraido)
                    .Set(d => d.UltimoFlujo, null); // reset flujo al subir nueva versión
                await _mongoCollection.UpdateOneAsync(filtroMongo, updateMongo);

                await RegistrarLogAsync(idUsuario, id, "NUEVA VERSIÓN", $"v{nuevaVersion} - {datos.ComentarioCambio}");

                return Ok(new
                {
                    Mensaje          = $"Versión {nuevaVersion} subida. El documento regresa a Borrador.",
                    IdDocumento      = id,
                    NuevaVersion     = nuevaVersion,
                    EstadoActual     = "Borrador",
                    ComentarioCambio = datos.ComentarioCambio,
                    SiguientePaso    = $"Solicita aprobación en: PUT /api/Documentos/solicitar-aprobacion/{id}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al subir la nueva versión.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/{id}/versiones
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpGet("{id}/versiones")]
        public async Task<IActionResult> ObtenerVersionesPorDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (_, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes ver el historial de versiones de documentos de otra área." });

                var versiones = new List<object>();

                const string sql = @"
                    SELECT
                        v.id_version,
                        v.numero_version,
                        v.nombre_archivo,
                        v.tamano_bytes,
                        v.comentario_cambio,
                        v.fecha_version,
                        u.nombre_completo AS nombre_usuario
                    FROM  VersionesDocumento v
                    INNER JOIN Usuarios u ON v.id_usuario = u.id_usuario
                    WHERE v.id_documento = @id
                    ORDER BY v.numero_version DESC";

                using (var con = new SqlConnection(_config.GetConnectionString("SqlConexion")))
                {
                    con.Open();
                    using var cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", id);
                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        versiones.Add(new
                        {
                            IdVersion        = (int)rd["id_version"],
                            NumeroVersion    = (short)rd["numero_version"],
                            NombreArchivo    = rd["nombre_archivo"].ToString(),
                            TamanoBytes      = rd["tamano_bytes"]      == DBNull.Value ? (long?)null : (long)rd["tamano_bytes"],
                            ComentarioCambio = rd["comentario_cambio"] == DBNull.Value ? null        : rd["comentario_cambio"].ToString(),
                            FechaVersion     = (DateTime)rd["fecha_version"],
                            SubidoPor        = rd["nombre_usuario"].ToString()
                        });
                    }
                }

                return Ok(new
                {
                    IdDocumento     = id,
                    TituloDocumento = documento.Titulo,
                    VersionActual   = documento.NumeroVersion,
                    EstadoActual    = documento.IdEstado switch { 1 => "Borrador", 2 => "Aprobado", 3 => "Obsoleto", _ => "Desconocido" },
                    TotalVersiones  = versiones.Count,
                    Versiones       = versiones
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al obtener las versiones.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/{id}/versiones/{numeroVersion}/descargar
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpGet("{id}/versiones/{numeroVersion}/descargar")]
        public async Task<IActionResult> DescargarVersionEspecifica(int id, short numeroVersion)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                if (!TieneAccesoParaVer(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "Este documento no pertenece a tu área." });

                var version = _sqlContext.VersionesDocumento
                    .FirstOrDefault(v => v.IdDocumento == id && v.NumeroVersion == numeroVersion);

                if (version == null)
                    return NotFound(new { Mensaje = $"No existe la versión {numeroVersion} para este documento." });

                var memoryStream = new MemoryStream();

                if (version.RutaArchivo != "Sin archivo físico" && !string.IsNullOrWhiteSpace(version.RutaArchivo))
                {
                    var sftpSettings = _config.GetSection("SftpConfig");
                    using (var client = new SftpClient(
                        sftpSettings["Host"]     ?? "localhost",
                        int.TryParse(sftpSettings["Port"], out int p) ? p : 2222,
                        sftpSettings["Username"] ?? "sarahi",
                        sftpSettings["Password"] ?? "12345"))
                    {
                        client.Connect();
                        client.DownloadFile(version.RutaArchivo, memoryStream);
                        client.Disconnect();
                    }
                    memoryStream.Position = 0;

                    string nombreBonito = version.NombreArchivo;
                    int    idx          = nombreBonito.IndexOf('_');
                    nombreBonito = idx >= 0
                        ? $"v{numeroVersion}_{nombreBonito.Substring(idx + 1)}"
                        : $"v{numeroVersion}_{documento.Titulo}.pdf";

                    await RegistrarLogAsync(idUsuario, id, "DESCARGÓ", $"v{numeroVersion} - {nombreBonito}");
                    return File(memoryStream, "application/pdf", nombreBonito);
                }
                else
                {
                    string titulo    = string.IsNullOrWhiteSpace(documento.Titulo) ? "Documento Sin Título" : documento.Titulo;
                    string contenido = string.IsNullOrWhiteSpace(version.ComentarioCambio) ? "Versión sin contenido." : version.ComentarioCambio;

                    string html = $@"
                        <html><head><style>
                            body {{ font-family: 'Helvetica', sans-serif; padding: 30px; color: #333; }}
                            h1   {{ color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px; }}
                            .version-badge {{ text-align:center; margin: 8px 0 20px; color: #7f8c8d; font-size: 10pt; }}
                            .contenido {{ font-size: 12pt; line-height: 1.6; margin-top: 20px; }}
                            .footer {{ margin-top: 50px; font-size: 8pt; color: #7f8c8d; text-align: center; border-top: 1px solid #eee; padding-top: 10px; }}
                        </style></head><body>
                            <h1>{titulo}</h1>
                            <div class='version-badge'>Versión {numeroVersion} — {version.FechaVersion:dd/MM/yyyy}</div>
                            <div class='contenido'>{contenido}</div>
                            <div class='footer'>Documento generado por QualityDoc System - {DateTime.Now:dd/MM/yyyy}</div>
                        </body></html>";

                    byte[] pdfBytes;
                    using (var ms = new MemoryStream())
                    {
                        HtmlConverter.ConvertToPdf(html, ms);
                        pdfBytes = ms.ToArray();
                    }

                    var nombre = $"v{numeroVersion}_{string.Join("_", titulo.Split(Path.GetInvalidFileNameChars()))}.pdf";
                    await RegistrarLogAsync(idUsuario, id, "DESCARGÓ", $"v{numeroVersion} - {nombre}");
                    return File(pdfBytes, "application/pdf", nombre);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al descargar la versión.", Detalle = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Documentos/ver-todo-mongo — debug en desarrollo
        // ─────────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("ver-todo-mongo")]
        public async Task<IActionResult> VerTodoMongo()
        {
            try
            {
                var todos = await _mongoCollection.Find(_ => true).ToListAsync();
                return Ok(todos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error en Mongo", Detalle = ex.Message });
            }
        }
    }
}