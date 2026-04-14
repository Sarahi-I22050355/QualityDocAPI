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
using Microsoft.AspNetCore.Authorization;
// Nuevas librerías para la generación de PDF profesional
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Html2pdf; // <-- NUEVA LIBRERÍA PARA TRADUCIR HTML A PDF
using System.Security.Claims; // Añadido para acceder a ClaimTypes de forma limpia

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

        // POST: api/Documentos
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPost]
        public async Task<IActionResult> SubirDocumento([FromForm] NuevoDocumentoDTO datosDePantalla)
        {
            if (datosDePantalla == null)
            {
                return BadRequest(new { Mensaje = "No se recibieron datos en el formulario." });
            }

            bool tieneArchivo = datosDePantalla.Archivo != null && datosDePantalla.Archivo.Length > 0;
            bool tieneTexto = !string.IsNullOrWhiteSpace(datosDePantalla.ContenidoTexto);

            if (!tieneArchivo && !tieneTexto)
            {
                return BadRequest(new { Mensaje = "Debes escribir el contenido o adjuntar un archivo PDF." });
            }

            string textoExtraido = "";
            string rutaFinalEnLinux = "Sin archivo físico";
            string nombreArchivoFinal = "N/A";

            try
            {
                if (tieneArchivo)
                {
                    using (var stream = datosDePantalla.Archivo.OpenReadStream())
                    {
                        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(stream))                        
                        {
                            foreach (var page in pdf.GetPages())
                            {
                                textoExtraido += page.Text + " ";
                            }
                        }
                    }

                    var sftpSettings = _config.GetSection("SftpConfig");
                    nombreArchivoFinal = $"{Guid.NewGuid()}_{datosDePantalla.Archivo.FileName}";
                    rutaFinalEnLinux = $"{sftpSettings["RemotePath"]}/{nombreArchivoFinal}";

                    using (var client = new SftpClient(
                        sftpSettings["Host"], 
                        int.Parse(sftpSettings["Port"]), 
                        sftpSettings["Username"], 
                        sftpSettings["Password"]))
                    {
                        client.Connect();
                        using (var uploadStream = datosDePantalla.Archivo.OpenReadStream())
                        {
                            client.UploadFile(uploadStream, rutaFinalEnLinux);
                        }
                        client.Disconnect();
                    }
                }
                else
                {
                    textoExtraido = datosDePantalla.ContenidoTexto;
                }

                var idUsuarioGafete = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int idUsuarioReal = int.TryParse(idUsuarioGafete, out int idParseado) ? idParseado : 0;

                string nombreCategoriaMongo = datosDePantalla.IdCategoria switch
                {
                    1 => "Manual de Calidad",
                    2 => "Procedimientos",
                    3 => "Instrucciones",
                    4 => "Formatos",
                    5 => "Registros",
                    6 => "Ayudas visuales",
                    _ => "Categoría General"
                };

                var documentoParaSQL = new DocumentoSQL
                {
                    Titulo = datosDePantalla.Titulo,
                    Descripcion = tieneArchivo ? "Documento cargado vía PDF" : datosDePantalla.ContenidoTexto,
                    IdUsuario = idUsuarioReal, 
                    IdCategoria = datosDePantalla.IdCategoria,
                    IdEstado = 1, // <--- Estado 1: "En Revisión" por defecto     
                    RutaArchivo = rutaFinalEnLinux,
                    NombreArchivo = nombreArchivoFinal,
                    Extension = tieneArchivo ? "pdf" : "txt"
                };

                _sqlContext.Documentos.Add(documentoParaSQL);
                await _sqlContext.SaveChangesAsync();

                var documentoParaMongo = new DocumentoMongo
                {
                    SqlId = documentoParaSQL.Id,
                    Titulo = datosDePantalla.Titulo,
                    Descripcion = textoExtraido,
                    Categoria = nombreCategoriaMongo,
                    Autor = datosDePantalla.Autor,
                    Extension = tieneArchivo ? "pdf" : "txt",
                    Etiquetas = datosDePantalla.Etiquetas?
                                .Where(e => !string.IsNullOrWhiteSpace(e)) 
                                .Select(e => e.ToLower())
                                .ToArray() ?? Array.Empty<string>()
                };

                await _mongoCollection.InsertOneAsync(documentoParaMongo);

                return Ok(new
                {
                    Mensaje = tieneArchivo ? "¡PDF subido a Linux y procesado!" : "¡Texto guardado correctamente!",
                    IdGeneradoEnSQL = documentoParaSQL.Id,
                    Ubicacion = rutaFinalEnLinux
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Ocurrió un error al procesar el documento.", Detalle = ex.Message });
            }
        }

        // GET: api/Documentos/buscar/{palabraClave}
        [Authorize] 
        [HttpGet("buscar/{palabraClave}")]
        public async Task<IActionResult> BuscarDocumentos(string palabraClave)
        {
            var busquedaNormalizada = palabraClave.ToLower();
            try
            {
                var builder = Builders<DocumentoMongo>.Filter;
                
                // --- BÚSQUEDA NORMAL EN MONGO (INCLUYE AUTOR) ---
                var filtro = builder.Or(
                    builder.AnyEq(d => d.Etiquetas, busquedaNormalizada),
                    builder.Regex(d => d.Titulo, new MongoDB.Bson.BsonRegularExpression(busquedaNormalizada, "i")),
                    builder.Regex(d => d.Descripcion, new MongoDB.Bson.BsonRegularExpression(busquedaNormalizada, "i")),
                    builder.Regex(d => d.Autor, new MongoDB.Bson.BsonRegularExpression(busquedaNormalizada, "i")) 
                );

                var resultadosMongo = await _mongoCollection.Find(filtro).ToListAsync();

                if (resultadosMongo.Count == 0)
                {
                    return NotFound(new { Mensaje = $"No hay resultados para: '{busquedaNormalizada}'" });
                }

                // --- LÓGICA NUEVA: MANEJO DE RESULTADOS SEGÚN EL ROL ---
                
                // Obtenemos qué rol está haciendo la búsqueda
                var rolUsuario = User.FindFirst("idRol")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                // Extraemos los IDs de SQL para buscar sus estados reales
                var idsMongo = resultadosMongo.Select(r => r.SqlId).ToList();
                var documentosSQL = _sqlContext.Documentos.Where(d => idsMongo.Contains(d.Id)).ToList();

                // LÓGICA PARA OPERARIOS (Rol 3)
                if (rolUsuario == "3")
                {
                    // Solo mostramos los que tienen IdEstado == 2 (Aprobados)
                    var idsAprobados = documentosSQL.Where(d => d.IdEstado == 2).Select(d => d.Id).ToList();
                    var resultadosFinales = resultadosMongo.Where(m => idsAprobados.Contains(m.SqlId)).ToList();

                    if (resultadosFinales.Count == 0)
                        return NotFound(new { Mensaje = "No hay documentos aprobados que coincidan con tu búsqueda." });

                    return Ok(new { Mensaje = "Búsqueda exitosa", Resultados = resultadosFinales });
                }
                
                // LÓGICA PARA ADMINS (Rol 1) Y SUPERVISORES (Rol 2)
                else
                {
                    // Formateamos la respuesta para incluir el estado actual del documento
                    var resultadosConEstado = resultadosMongo.Select(docMongo => 
                    {
                        var docSql = documentosSQL.FirstOrDefault(d => d.Id == docMongo.SqlId);
                        string nombreEstado = docSql?.IdEstado switch
                        {
                            1 => "En Revisión",
                            2 => "Aprobado",
                            3 => "Obsoleto",
                            _ => "Desconocido"
                        };

                        return new { Documento = docMongo, Estado = nombreEstado };
                    }).ToList();

                    return Ok(new { Mensaje = "Búsqueda exitosa", Resultados = resultadosConEstado });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error de conexión", Detalle = ex.Message });
            }
        }

        // PUT: api/Documentos/aprobar/{id}
        [Authorize(Policy = "SubeYAprueba")]
        [HttpPut("aprobar/{id}")]
        public async Task<IActionResult> AprobarDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null) return NotFound(new { Mensaje = "Documento no encontrado." });
                if (documento.IdEstado == 2) return BadRequest(new { Mensaje = "Ya está aprobado." });

                documento.IdEstado = 2; // Estado 2 es "Aprobado"
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = "¡Documento aprobado!", IdDocumento = documento.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al aprobar", Detalle = ex.Message });
            }
        }

        // GET: api/Documentos/descargar/{id}
        [Authorize] 
        [HttpGet("descargar/{id}")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null) return NotFound(new { Mensaje = "Documento no encontrado." });

                // --- LÓGICA NUEVA: BLOQUEO DE SEGURIDAD PARA OPERARIOS ---
                var rolUsuario = User.FindFirst("idRol")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
                
                // Si es operario (3) y el documento NO está aprobado (2), bloqueamos la descarga
                if (rolUsuario == "3" && documento.IdEstado != 2)
                {
                    return StatusCode(403, new { Mensaje = "Acceso denegado: Solo puedes descargar documentos aprobados." });
                }
                // ----------------------------------------------------------

                var memoryStream = new MemoryStream();

                // CASO 1: Archivo físico PDF en servidor Linux
                if (documento.RutaArchivo != "Sin archivo físico")
                {
                    var sftpSettings = _config.GetSection("SftpConfig");
                    using (var client = new SftpClient(
                        sftpSettings["Host"], 
                        int.Parse(sftpSettings["Port"]), 
                        sftpSettings["Username"], 
                        sftpSettings["Password"]))
                    {
                        client.Connect();
                        client.DownloadFile(documento.RutaArchivo, memoryStream);
                        client.Disconnect();
                    }
                    
                    memoryStream.Position = 0; 

                    string nombreBonito = documento.NombreArchivo;
                    int indiceGuionBajo = nombreBonito.IndexOf('_');
                    
                    if (indiceGuionBajo >= 0)
                    {
                        nombreBonito = nombreBonito.Substring(indiceGuionBajo + 1);
                    }
                    else
                    {
                        nombreBonito = $"{documento.Titulo}.pdf";
                    }

                    return File(memoryStream, "application/pdf", nombreBonito);
                }
                
                // CASO 2: Texto en BD -> Generación de PDF con Formato Profesional (HTML a PDF)
                else
                {
                    string tituloTexto = string.IsNullOrWhiteSpace(documento.Titulo) ? "Documento Sin Titulo" : documento.Titulo;
                    string contenidoTexto = string.IsNullOrWhiteSpace(documento.Descripcion) ? "El documento no tiene contenido." : documento.Descripcion;
                    
                    // Creamos la plantilla HTML con el texto que vino de la base de datos
                    string htmlElegante = $@"
                        <html>
                        <head>
                            <style>
                                body {{ font-family: 'Helvetica', sans-serif; padding: 30px; color: #333; }}
                                h1 {{ color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px; }}
                                .contenido {{ font-size: 12pt; line-height: 1.6; margin-top: 20px; }}
                                .footer {{ margin-top: 50px; font-size: 8pt; color: #7f8c8d; text-align: center; border-top: 1px solid #eee; padding-top: 10px; }}
                            </style>
                        </head>
                        <body>
                            <h1>{tituloTexto}</h1>
                            <div class='contenido'>
                                {contenidoTexto}
                            </div>
                            <div class='footer'>
                                Documento generado automáticamente por QualityDoc System - {DateTime.Now:dd/MM/yyyy}
                            </div>
                        </body>
                        </html>";

                    byte[] pdfBytes;

                    using (var msPdf = new MemoryStream())
                    {
                        // La magia ocurre aquí: Convertimos el HTML directamente al PDF
                        HtmlConverter.ConvertToPdf(htmlElegante, msPdf);
                        pdfBytes = msPdf.ToArray();
                    }

                    var nombreArchivoPdf = string.Join("_", tituloTexto.Split(Path.GetInvalidFileNameChars())) + ".pdf";

                    return File(pdfBytes, "application/pdf", nombreArchivoPdf);
                }
             }
            catch (Exception ex)
            {
                string causaOculta = ex.InnerException != null ? ex.InnerException.Message : "No hay error interno detallado.";
                
                return StatusCode(500, new 
                { 
                    Error = "Error en la descarga", 
                    Detalle = ex.Message,
                    CausaReal = causaOculta,
                    Pista = ex.StackTrace?.Split('\n').FirstOrDefault() 
                });
            }
        }

        [AllowAnonymous] 
        [HttpGet("ver-todo-mongo")]
        public async Task<IActionResult> VerTodoMongo()
        {
            try
            {
                var todosLosDocumentos = await _mongoCollection.Find(_ => true).ToListAsync();
                return Ok(todosLosDocumentos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error en Mongo", Detalle = ex.Message });
            }
        }
    }
}