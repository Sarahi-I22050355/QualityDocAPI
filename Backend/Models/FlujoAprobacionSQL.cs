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

        // Documento que está siendo revisado
        [Column("id_documento")]
        public int IdDocumento { get; set; }

        // Usuario que pidió la revisión (Admin o Supervisor que subió o gestiona el doc)
        [Column("id_solicitante")]
        public int IdSolicitante { get; set; }

        // Usuario que resolvió (aprobó o rechazó). Null mientras esté Pendiente.
        [Column("id_revisor")]
        public int? IdRevisor { get; set; }

        // "Pendiente" al crearse. Cambia a "Aprobado" o "Rechazado" al resolver.
        [Column("decision")]
        public string Decision { get; set; } = "Pendiente";

        // Comentario del revisor al aprobar o rechazar. Obligatorio al rechazar (buena práctica).
        [Column("comentarios")]
        public string? Comentarios { get; set; }

        [Column("fecha_solicitud")]
        public DateTime FechaSolicitud { get; set; } = DateTime.Now;

        // Null mientras Decision = "Pendiente". Se llena al resolver.
        [Column("fecha_resolucion")]
        public DateTime? FechaResolucion { get; set; }
    }
}