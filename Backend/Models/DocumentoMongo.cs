using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QualityDocAPI.Models
{
    // Información del último movimiento del flujo de aprobación
    public class UltimoFlujoMongo
    {
        [BsonElement("decision")]
        public string Decision { get; set; } = string.Empty;

        [BsonElement("revisado_por")]
        public string RevisadoPor { get; set; } = string.Empty;

        [BsonElement("fecha_resolucion")]
        public DateTime FechaResolucion { get; set; }
    }

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

        [BsonElement("area")]
        public string Area { get; set; } = string.Empty;

        // ── Campos de auditoría ─────────────────────────────────────
        [BsonElement("subido_por")]
        public string? SubidoPor { get; set; }

        [BsonElement("fecha_subida")]
        public DateTime? FechaSubida { get; set; }

        // Último movimiento del flujo (Pendiente / Aprobado / Rechazado)
        [BsonElement("ultimo_flujo")]
        public UltimoFlujoMongo? UltimoFlujo { get; set; }
    }
}