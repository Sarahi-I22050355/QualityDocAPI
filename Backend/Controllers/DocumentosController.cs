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
        // Si el log falla, NO interrumpe la operación principal.
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
        // ACCESO PARA VER / DESCARGAR (lectura)
        //
        // Permite acceso si:
        //   - Es Admin (rol 1)             → acceso total
        //   - Usuario del área General     → acceso total (esGeneral = true en token)
        //   - Documento es de su área      → acceso permitido
        //   - Documento pertenece a un área marcada como es_general en BD
        //                                  → cualquiera puede verlo/descargarlo
        //
        // Usado en: BuscarDocumentos, DescargarDocumento, ObtenerFlujoPorDocumento
        // ─────────────────────────────────────────────────────────────────
        private bool TieneAccesoParaVer(string rol, bool esGeneral, int? idAreaToken, int? idAreaDocumento)
        {
            if (rol == "1" || esGeneral) return true;
            if (idAreaDocumento == idAreaToken) return true;

            // ¿El documento pertenece a un área marcada como General en la BD?
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
        //
        // Más estricto que el de lectura. Permite acceso si:
        //   - Es Admin (rol 1)             → acceso total
        //   - Usuario del área General     → acceso total (esGeneral = true en token)
        //   - Documento es de SU MISMO área → acceso permitido
        //
        // NO permite acceso aunque el documento sea de un área General:
        // un Supervisor de Calidad puede VER documentos del área General,
        // pero NO puede aprobarlos, versionarlos ni solicitar su aprobación.
        // Esas acciones las reservamos para el Admin o el Supervisor asignado
        // al área General.
        //
        // Usado en: SolicitarAprobacion, ResolverAprobacion,
        //           SubirNuevaVersion, ObtenerVersionesPorDocumento
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
        // Reglas de área:
        //   - Supervisor: el área del doc se hereda de su token.
        //   - Admin: puede especificar IdArea libremente.
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

            int?   idAreaDocumento;
            string nombreAreaDocumento;

            if (rol == "2") // Supervisor: hereda su propio área del token
            {
                idAreaDocumento     = idAreaToken;
                nombreAreaDocumento = nombreArea;
            }
            else // Admin: puede elegir área libremente
            {
                if (datosDePantalla.IdArea.HasValue && datosDePantalla.IdArea.Value > 0)
                {
                    var areaElegida = await _sqlContext.Areas.FindAsync(datosDePantalla.IdArea.Value);
                    if (areaElegida == null || !areaElegida.Activo)
                        return BadRequest(new { Mensaje = $"El área con ID {datosDePantalla.IdArea} no existe." });
                    idAreaDocumento     = areaElegida.Id;
                    nombreAreaDocumento = areaElegida.Nombre;
                }
                else
                {
                    idAreaDocumento     = idAreaToken;
                    nombreAreaDocumento = nombreArea;
                }
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
                    string remotePath  = sftpSettings["RemotePath"] ?? "/home/sarahi/uploads";
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

                await _mongoCollection.InsertOneAsync(new DocumentoMongo
                {
                    SqlId       = documentoSQL.Id,
                    Titulo      = datosDePantalla.Titulo,
                    Descripcion = textoExtraido,
                    Categoria   = nombreCategoriaMongo,
                    Autor       = datosDePantalla.Autor,
                    Extension   = tieneArchivo ? "pdf" : "txt",
                    Area        = nombreAreaDocumento,
                    Etiquetas   = datosDePantalla.Etiquetas?
                                    .Where(e => !string.IsNullOrWhiteSpace(e))
                                    .Select(e => e.ToLower())
                                    .ToArray() ?? Array.Empty<string>()
                });

                return Ok(new
                {
                    Mensaje         = tieneArchivo ? "¡PDF subido y procesado!" : "¡Texto guardado correctamente!",
                    IdGeneradoEnSQL = documentoSQL.Id,
                    Version         = 1,
                    Area            = nombreAreaDocumento,
                    Ubicacion       = rutaFinalEnLinux,
                    SiguientePaso   = $"Cuando el documento esté listo, solicita su aprobación en: PUT /api/Documentos/solicitar-aprobacion/{documentoSQL.Id}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al procesar el documento.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/buscar/{palabraClave}
        // Usa TieneAccesoParaVer (lectura amplia).
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

                if (esGeneral)
                {
                    filtroFinal = filtroTexto;
                }
                else
                {
                    // Obtener los nombres de todas las áreas Generales de la BD
                    var nombresAreasGenerales = _sqlContext.Areas
                        .Where(a => a.EsGeneral && a.Activo)
                        .Select(a => a.Nombre)
                        .ToList();

                    // Su área + cualquier área General
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

                if (rol == "3") // Operario: solo documentos Aprobados
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
        // PUT: api/Documentos/solicitar-aprobacion/{id}
        // PASO 1 del flujo de aprobación.
        // Usa TieneAccesoParaModificar (escritura estricta):
        //   - Supervisor de Calidad NO puede solicitar aprobación de un doc
        //     del área General aunque pueda verlo.
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

                // ── Acceso estricto: Supervisor solo puede gestionar SU área ──
                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes solicitar aprobación para documentos de otra área. Los documentos del área General solo pueden ser gestionados por el Admin o el Supervisor General." });

                bool yaExistePendiente = _sqlContext.FlujoAprobacion
                    .Any(f => f.IdDocumento == id && f.Decision == "Pendiente");
                if (yaExistePendiente)
                    return BadRequest(new { Mensaje = "Este documento ya tiene una solicitud de aprobación pendiente. Espera a que un revisor la resuelva." });

                var flujo = new FlujoAprobacionSQL
                {
                    IdDocumento    = id,
                    IdSolicitante  = idUsuario,
                    Decision       = "Pendiente",
                    FechaSolicitud = DateTime.Now
                };
                _sqlContext.FlujoAprobacion.Add(flujo);
                await _sqlContext.SaveChangesAsync();

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
        // PASO 2 del flujo de aprobación.
        // Usa TieneAccesoParaModificar (escritura estricta):
        //   - Supervisor de Calidad NO puede aprobar un doc del área General.
        // Body: { "Decision": "Aprobado", "Comentarios": "..." }
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
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
                    return NotFound(new { Mensaje = "El documento asociado a esta solicitud no existe." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                // ── Acceso estricto: Supervisor solo puede resolver flujos de SU área ──
                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes resolver aprobaciones de documentos de otra área. Los documentos del área General solo pueden ser aprobados por el Admin o el Supervisor General." });

                flujo.IdRevisor       = idUsuario;
                flujo.Decision        = datos.Decision;
                flujo.Comentarios     = datos.Comentarios;
                flujo.FechaResolucion = DateTime.Now;

                documento.IdEstado          = datos.Decision == "Aprobado" ? 2 : 1;
                documento.FechaModificacion = DateTime.Now;

                await _sqlContext.SaveChangesAsync();

                string accionLog = datos.Decision == "Aprobado" ? "APROBÓ" : "RECHAZÓ";
                await RegistrarLogAsync(idUsuario, documento.Id, accionLog, datos.Comentarios ?? documento.Titulo);

                string mensajeResultado = datos.Decision == "Aprobado"
                    ? "Documento aprobado. Ya está disponible para todos los usuarios del sistema."
                    : "Documento rechazado. Regresa a Borrador. El solicitante puede corregirlo y pedir revisión nuevamente.";

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
        // Historial del flujo de aprobación.
        // Usa TieneAccesoParaVer (lectura amplia):
        //   - Supervisor puede ver el historial de un doc del área General,
        //     aunque no pueda modificarlo.
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

                // Lectura amplia: puede ver el historial si tiene acceso al doc
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
        // Usa TieneAccesoParaVer (lectura amplia):
        //   - Supervisores y Operarios pueden descargar docs del área General.
        //   - Operarios solo si el doc está Aprobado.
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

                // Lectura amplia: su área + áreas Generales de la BD
                if (!TieneAccesoParaVer(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "Este documento no pertenece a tu área." });

                // Operarios: solo documentos Aprobados
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
                return StatusCode(500, new
                {
                    Error     = "Error en la descarga",
                    Detalle   = ex.Message,
                    CausaReal = ex.InnerException?.Message ?? "Sin error interno.",
                    Pista     = ex.StackTrace?.Split('\n').FirstOrDefault()
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST: api/Documentos/{id}/nueva-version
        // Usa TieneAccesoParaModificar (escritura estricta):
        //   - Supervisor de Calidad NO puede versionar un doc del área General.
        // ═══════════════════════════════════════════════════════════════════
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPost("{id}/nueva-version")]
        public async Task<IActionResult> SubirNuevaVersion(int id, [FromForm] NuevaVersionDTO datos)
        {
            bool tieneArchivo = datos.Archivo != null && datos.Archivo.Length > 0;
            bool tieneTexto   = !string.IsNullOrWhiteSpace(datos.ContenidoTexto);

            if (!tieneArchivo && !tieneTexto)
                return BadRequest(new { Mensaje = "Debes adjuntar un archivo PDF o escribir el contenido del documento." });

            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null)
                    return NotFound(new { Mensaje = "Documento no encontrado." });

                var (idUsuario, rol, esGeneral, idAreaToken) = ObtenerDatosToken();

                // ── Acceso estricto: Supervisor solo puede versionar SU área ──
                if (!TieneAccesoParaModificar(rol, esGeneral, idAreaToken, documento.IdArea))
                    return StatusCode(403, new { Mensaje = "No puedes subir versiones de documentos de otra área. Los documentos del área General solo pueden ser modificados por el Admin o el Supervisor General." });

                bool hayPendiente = _sqlContext.FlujoAprobacion
                    .Any(f => f.IdDocumento == id && f.Decision == "Pendiente");
                if (hayPendiente)
                    return BadRequest(new { Mensaje = "Hay una solicitud de aprobación pendiente para este documento. Resuélvela antes de subir una nueva versión." });

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
                    string remotePath  = sftpSettings["RemotePath"] ?? "/home/sarahi/uploads";
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
                var updateMongo = Builders<DocumentoMongo>.Update.Set(d => d.Descripcion, textoExtraido);
                await _mongoCollection.UpdateOneAsync(filtroMongo, updateMongo);

                await RegistrarLogAsync(idUsuario, id, "NUEVA VERSIÓN", $"v{nuevaVersion} - {datos.ComentarioCambio}");

                return Ok(new
                {
                    Mensaje          = $"Versión {nuevaVersion} subida correctamente. El documento regresa a Borrador para un nuevo ciclo de aprobación.",
                    IdDocumento      = id,
                    NuevaVersion     = nuevaVersion,
                    EstadoActual     = "Borrador",
                    ComentarioCambio = datos.ComentarioCambio,
                    SiguientePaso    = $"Solicita aprobación de la versión {nuevaVersion} en: PUT /api/Documentos/solicitar-aprobacion/{id}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al subir la nueva versión.", Detalle = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET: api/Documentos/{id}/versiones
        // Usa TieneAccesoParaModificar (escritura estricta):
        //   - Ver el historial de versiones es una operación sensible de auditoría,
        //     reservada al área propietaria del documento y al Admin.
        //   - Un Supervisor de otra área puede ver el documento pero no su
        //     historial interno de versiones.
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

                // Acceso estricto: el historial de versiones es de gestión interna del área
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

        // ─────────────────────────────────────────────────────────────────
        // GET: api/Documentos/ver-todo-mongo — solo para debug en desarrollo.
        // Antes de producción cambiar [AllowAnonymous] por [Authorize(Policy = "SoloAdmin")]
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