using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class ResolverAprobacionDTO
    {
        // Solo acepta exactamente "Aprobado" o "Rechazado". Se valida en el controller.
        [Required(ErrorMessage = "La decisión es obligatoria.")]
        public string Decision { get; set; } = string.Empty;

        // Obligatorio al rechazar (buena práctica de auditoría). Opcional al aprobar.
        public string? Comentarios { get; set; }
    }
}