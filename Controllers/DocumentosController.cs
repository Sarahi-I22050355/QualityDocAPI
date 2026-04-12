using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Models;
using QualityDocAPI.DTOs;
using QualityDocAPI.Data;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Linq;
using UglyToad.PdfPig;
using System.IO;
using Microsoft.Extensions.Configuration; // Para leer appsettings.json
using Renci.SshNet;                       // Para el túnel SFTP
using System;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentosController : ControllerBase
    {
        private readonly SqlContext _sqlContext;
        private readonly IMongoCollection<DocumentoMongo> _mongoCollection;
        private readonly IConfiguration _config; // La "llave" para las contraseñas

        // Actualizamos el constructor para recibir IConfiguration
        public DocumentosController(SqlContext sqlContext, IMongoClient mongoClient, IConfiguration config)
        {
            _sqlContext = sqlContext;
            _config = config;
            var database = mongoClient.GetDatabase("QualityDocPolyglotDB");
            _mongoCollection = database.GetCollection<DocumentoMongo>("documentosBusqueda");
        }

        // POST: api/Documentos
        [HttpPost]
        // Cambiamos [FromBody] a [FromForm] para que acepte archivos
        public async Task<IActionResult> SubirDocumento([FromForm] NuevoDocumentoDTO datosDePantalla)
        {
            // 1. Verificación del modo híbrido
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
                // 2. Si el usuario subió un archivo, lo procesamos y mandamos a Linux
                if (tieneArchivo)
                {
                    // A. Extraer texto del PDF para la búsqueda en Mongo
                    using (var stream = datosDePantalla.Archivo.OpenReadStream())
                    {
                        using (var pdf = PdfDocument.Open(stream))
                        {
                            foreach (var page in pdf.GetPages())
                            {
                                textoExtraido += page.Text + " ";
                            }
                        }
                    }

                    // B. Configuración SFTP (Leyendo de tu appsettings.json)
                    var sftpSettings = _config.GetSection("SftpConfig");
                    nombreArchivoFinal = $"{Guid.NewGuid()}_{datosDePantalla.Archivo.FileName}";
                    rutaFinalEnLinux = $"{sftpSettings["RemotePath"]}/{nombreArchivoFinal}";

                    // C. Conexión y subida mediante el túnel (puerto 2222)
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
                    // Si no hay archivo, el texto es simplemente lo que escribió
                    textoExtraido = datosDePantalla.ContenidoTexto;
                }

                // 3. Preparar el modelo para SQL Server
                var documentoParaSQL = new DocumentoSQL
                {
                    Titulo = datosDePantalla.Titulo,
                    Descripcion = tieneArchivo ? "Documento cargado vía PDF" : datosDePantalla.ContenidoTexto,
                    IdUsuario = 2,    // Laura Supervisora
                    IdCategoria = 1,  // Manual de Calidad
                    IdEstado = 1,     // Borrador
                    RutaArchivo = rutaFinalEnLinux,
                    NombreArchivo = nombreArchivoFinal,
                    Extension = tieneArchivo ? "pdf" : "txt"
                };

                // Guardar en SQL
                _sqlContext.Documentos.Add(documentoParaSQL);
                await _sqlContext.SaveChangesAsync();

                // 4. Preparar el modelo para MongoDB
                var documentoParaMongo = new DocumentoMongo
                {
                    SqlId = documentoParaSQL.Id, // Vinculamos con el ID de SQL
                    Titulo = datosDePantalla.Titulo,
                    Descripcion = textoExtraido, // Aquí va todo el texto del PDF o del cuadro
                    Categoria = "Manual de Calidad",
                    Autor = datosDePantalla.Autor,
                    Extension = tieneArchivo ? "pdf" : "txt",
                    Etiquetas = datosDePantalla.Etiquetas?.Select(e => e.ToLower()).ToArray() ?? Array.Empty<string>()
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
                return StatusCode(500, new
                {
                    Error = "Ocurrió un error al procesar el documento.",
                    Detalle = ex.Message
                });
            }
        }

        // GET: api/Documentos/buscar/{palabraClave}
        [HttpGet("buscar/{palabraClave}")]
        public async Task<IActionResult> BuscarDocumentos(string palabraClave)
        {
            var busquedaNormalizada = palabraClave.ToLower();

            try
            {
                // Buscamos en Mongo por etiquetas (ignora mayúsculas)
                var filtro = Builders<DocumentoMongo>.Filter.AnyEq(d => d.Etiquetas, busquedaNormalizada);
                var resultados = await _mongoCollection.Find(filtro).ToListAsync();

                if (resultados.Count == 0)
                {
                    return NotFound(new { Mensaje = $"No hay resultados para: '{busquedaNormalizada}'" });
                }

                return Ok(new
                {
                    Mensaje = "Búsqueda exitosa en BD real",
                    Resultados = resultados
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Error de conexión", Detalle = ex.Message });
            }
        }
    }
}