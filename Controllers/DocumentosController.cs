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
                        // CORRECCIÓN 1: Se especifica la ruta completa a UglyToad.PdfPig.PdfDocument
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
                    IdEstado = 1,     
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
                var filtro = builder.Or(
                    builder.AnyEq(d => d.Etiquetas, busquedaNormalizada),
                    builder.Regex(d => d.Titulo, new MongoDB.Bson.BsonRegularExpression(busquedaNormalizada, "i")),
                    builder.Regex(d => d.Descripcion, new MongoDB.Bson.BsonRegularExpression(busquedaNormalizada, "i"))
                );

                var resultados = await _mongoCollection.Find(filtro).ToListAsync();
                var rolUsuario = User.FindFirst("idRol")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (rolUsuario == "3")
                {
                    var idsAprobados = _sqlContext.Documentos
                        .Where(d => d.IdEstado == 2)
                        .Select(d => d.Id)
                        .ToList();
                    resultados = resultados.Where(m => idsAprobados.Contains(m.SqlId)).ToList();
                }

                if (resultados.Count == 0)
                {
                    return NotFound(new { Mensaje = $"No hay resultados para: '{busquedaNormalizada}'" });
                }

                return Ok(new { Mensaje = "Búsqueda exitosa", Resultados = resultados });
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

                documento.IdEstado = 2;
                await _sqlContext.SaveChangesAsync();

                return Ok(new { Mensaje = "¡Documento aprobado!", IdDocumento = documento.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error al aprobar", Detalle = ex.Message });
            }
        }

        // GET: api/Documentos/descargar/{id}
        // IMPLEMENTACIÓN MEJORADA: Descarga SIEMPRE en PDF
        [Authorize] 
        [HttpGet("descargar/{id}")]
        public async Task<IActionResult> DescargarDocumento(int id)
        {
            try
            {
                var documento = await _sqlContext.Documentos.FindAsync(id);
                if (documento == null) return NotFound(new { Mensaje = "Documento no encontrado." });

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
                    return File(memoryStream, "application/pdf", documento.NombreArchivo);
                }
                
                // CASO 2: Texto en BD -> Generación de PDF al vuelo con iText7
                else
                {
                    // 1. Validaciones ultra-seguras (Si vienen vacíos, iText explota por "Documento sin páginas")
                    string tituloTexto = string.IsNullOrWhiteSpace(documento.Titulo) ? "Documento Sin Titulo" : documento.Titulo;
                    string contenidoTexto = string.IsNullOrWhiteSpace(documento.Descripcion) ? "El documento no tiene contenido." : documento.Descripcion;
                    
                    byte[] pdfBytes;

                    // 2. Usar un stream limpio y exclusivo para iText
                    using (var msPdf = new MemoryStream())
                    {
                        var writer = new PdfWriter(msPdf);
                        var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
                        var document = new Document(pdf);

                        // 3. Quitamos la fuente personalizada temporalmente. Solo texto puro.
                        document.Add(new Paragraph(tituloTexto)
                            .SetFontSize(18)
                            .SetTextAlignment(TextAlignment.CENTER));
                            
                        document.Add(new Paragraph("\n")); // Salto de línea
                        
                        document.Add(new Paragraph(contenidoTexto)
                            .SetFontSize(11)
                            .SetTextAlignment(TextAlignment.JUSTIFIED));

                        // 4. Cerramos el documento
                        document.Close();
                        
                        // 5. Extraemos los bytes
                        pdfBytes = msPdf.ToArray();
                    }

                    var nombreArchivoPdf = string.Join("_", tituloTexto.Split(Path.GetInvalidFileNameChars())) + ".pdf";

                    return File(pdfBytes, "application/pdf", nombreArchivoPdf);
                }
             }

            catch (Exception ex)
            {
                // ATRAPAMOS EL ERROR REAL OCULTO:
                string causaOculta = ex.InnerException != null ? ex.InnerException.Message : "No hay error interno detallado.";
                
                return StatusCode(500, new 
                { 
                    Error = "Error en la descarga", 
                    Detalle = ex.Message,
                    CausaReal = causaOculta, // <-- Esto nos dirá la verdad
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