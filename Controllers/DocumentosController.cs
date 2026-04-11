using Microsoft.AspNetCore.Mvc;
using QualityDocAPI.Models;
using QualityDocAPI.DTOs;
using QualityDocAPI.Data; // IMPORTANTE: Para acceder al Contexto de SQL
using MongoDB.Driver;     // IMPORTANTE: Para acceder a MongoDB

namespace QualityDocAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentosController : ControllerBase
    {
        // Estas son las "llaves" de nuestras bases de datos
        private readonly SqlContext _sqlContext;
        private readonly IMongoCollection<DocumentoMongo> _mongoCollection;

        // EL CONSTRUCTOR: Aquí es donde la API nos entrega las llaves al iniciar
        public DocumentosController(SqlContext sqlContext, IMongoClient mongoClient)
        {
            _sqlContext = sqlContext;

            // Configuramos la conexión específica a la colección de Mongo
            var database = mongoClient.GetDatabase("QualityDocPolyglotDB");
            _mongoCollection = database.GetCollection<DocumentoMongo>("Documentos");
        }

        [HttpPost]
        public IActionResult SubirDocumento([FromBody] NuevoDocumentoDTO datosDePantalla)
        {
            // 1. Armamos el paquete para SQL Server
            var documentoParaSQL = new DocumentoSQL
            {
                Titulo = datosDePantalla.Titulo,
                Autor = datosDePantalla.Autor,
                Estado = "Borrador",
                FechaCreacion = DateTime.Now
            };

            // 2. Armamos el paquete para MongoDB
            var documentoParaMongo = new DocumentoMongo
            {
                Titulo = datosDePantalla.Titulo,
                ContenidoTexto = datosDePantalla.ContenidoTexto,
                Etiquetas = datosDePantalla.Etiquetas
            };

            // TODO: Mañana conectaremos las líneas de guardado real:
            // _sqlContext.Documentos.Add(documentoParaSQL);
            // _mongoCollection.InsertOne(documentoParaMongo);

            return Ok(new { 
                Mensaje = "¡Éxito! El controlador recibió y separó los datos correctamente.",
                DatosParaSQL = documentoParaSQL,
                DatosParaMongo = documentoParaMongo
            });
        }

        [HttpGet("buscar/{palabraClave}")]
        public IActionResult BuscarDocumentos(string palabraClave)
        {
            // NORMALIZACIÓN: Convertimos a minúsculas para que la búsqueda sea eficiente
            var busquedaNormalizada = palabraClave.ToLower();

            // TODO: Mañana usaremos '_mongoCollection' para buscar datos reales.
            var resultadosReales = new List<DocumentoMongo>(); 

            if (resultadosReales.Count == 0)
            {
                return Ok(new { 
                    Mensaje = $"No se encontró ningún documento con la etiqueta: '{busquedaNormalizada}'.", 
                    TotalEncontrados = 0,
                    Datos = resultadosReales 
                });
            }

            return Ok(new { 
                Mensaje = $"Búsqueda exitosa. Resultados para: '{busquedaNormalizada}'",
                TotalEncontrados = resultadosReales.Count,
                Datos = resultadosReales 
            });
        }
    }
}