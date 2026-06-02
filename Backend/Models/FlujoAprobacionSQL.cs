using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QualityDocAPI.Models
{
    [Table("FlujoAprobacion")]
    public class FlujoAprobacionSQL
    {
        [Key]
        [Column("id_flujo")]
        public int Id { get; set; }

        [Column("id_documento")]
        public int IdDocumento { get; set; }

        [Column("id_solicitante")]
        public int IdSolicitante { get; set; }

        [Column("id_revisor")]
        public int? IdRevisor { get; set; }

        // "Pendiente" al crearse. Cambia a "Aprobado" o "Rechazado" al resolver.
        [Column("decision")]
        public string Decision { get; set; } = "Pendiente";

        [Column("comentarios")]
        public string? Comentarios { get; set; }

        [Column("fecha_solicitud")]
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;

        [Column("fecha_resolucion")]
        public DateTime? FechaResolucion { get; set; }

        // Área que debe firmar este registro de flujo.
        // NULL = flujo legacy (antes del sistema de múltiples firmas)
        [Column("id_area_requerida")]
        public int? IdAreaRequerida { get; set; }
    }
}