using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Models;
using QualityDocAPI.DTOs;
using QualityDocAPI.Data;
using MongoDB.Driver;
using System.Threading.Tasks;
using System.Linq;

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentosController : ControllerBase
    {
        private readonly SqlContext _sqlContext;
        private readonly IMongoCollection<DocumentoMongo> _mongoCollection;

        public DocumentosController(SqlContext sqlContext, IMongoClient mongoClient)
        {
            _sqlContext = sqlContext;
            var database = mongoClient.GetDatabase("QualityDocPolyglotDB");
            _mongoCollection = database.GetCollection<DocumentoMongo>("documentosBusqueda");        }

        // POST: api/Documentos
        [HttpPost]
        public async Task<IActionResult> SubirDocumento([FromBody] NuevoDocumentoDTO datosDePantalla)
        {
            var documentoParaSQL = new DocumentoSQL
            {
                Titulo = datosDePantalla.Titulo,
                Descripcion = datosDePantalla.ContenidoTexto, // Usamos tu contenido como descripción temporalmente
                IdUsuario = 2,    // Laura Supervisora (de tus datos semilla)
                IdCategoria = 1,  // Manual de Calidad
                IdEstado = 1,     // Borrador
                RutaArchivo = "/uploads/temp/archivo_falso.pdf",
                NombreArchivo = "archivo_falso.pdf",
                Extension = "pdf"
            };
            var documentoParaMongo = new DocumentoMongo
            {
                Titulo = datosDePantalla.Titulo,
                Descripcion = datosDePantalla.ContenidoTexto,
                Categoria = "Manual de Calidad",
                Autor = datosDePantalla.Autor,
                Extension = "pdf",
                Etiquetas = datosDePantalla.Etiquetas.Select(e => e.ToLower()).ToArray()            
            };
            try 
            {
                // 1. Guardar en SQL Server (Metadatos)
                _sqlContext.Documentos.Add(documentoParaSQL);
                await _sqlContext.SaveChangesAsync();
                
                // Le pasamos el ID recién creado en SQL al documento de Mongo
                documentoParaMongo.SqlId = documentoParaSQL.Id;

                // 2. Guardar en MongoDB (Contenido pesado y etiquetas)
                // Usamos el ID de SQL para vincularlos si fuera necesario
                await _mongoCollection.InsertOneAsync(documentoParaMongo);

                return Ok(new { 
                    Mensaje = "¡Éxito total! Datos guardados en SQL y Mongo.",
                    IdGeneradoEnSQL = documentoParaSQL.Id 
                });
            }
            catch (Exception ex)
            {
                // Si Axel no ha prendido el servidor, caerás aquí:
                return StatusCode(500, new { 
                    Error = "No se pudo conectar con las bases de datos.", 
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
                // Buscamos en Mongo documentos que tengan la etiqueta (ignora mayúsculas)
                var filtro = Builders<DocumentoMongo>.Filter.AnyEq(d => d.Etiquetas, busquedaNormalizada);
                var resultados = await _mongoCollection.Find(filtro).ToListAsync();

                if (resultados.Count == 0)
                {
                    return NotFound(new { Mensaje = $"No hay resultados para: '{busquedaNormalizada}'" });
                }

                return Ok(new { 
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