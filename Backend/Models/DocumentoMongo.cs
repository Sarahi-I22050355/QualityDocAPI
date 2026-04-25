using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QualityDocAPI.Models
{
    public class DocumentoMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("sql_id")]
        public int SqlId { get; set; }

        [BsonElement("titulo")]
        public string Titulo { get; set; } = string.Empty;

        [BsonElement("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [BsonElement("categoria")]
        public string Categoria { get; set; } = string.Empty;

        [BsonElement("autor")]
        public string Autor { get; set; } = string.Empty;

        [BsonElement("extension")]
        public string Extension { get; set; } = "pdf";

        [BsonElement("etiquetas")]
        public string[] Etiquetas { get; set; } = Array.Empty<string>();

        // Nombre del área propietaria del documento. Se usa para filtrar búsquedas.
        // Los usuarios del área General ignoran este campo y ven todo.
        [BsonElement("area")]
        public string Area { get; set; } = string.Empty;
    }
}