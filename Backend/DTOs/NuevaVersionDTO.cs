using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class NuevaVersionDTO
    {
        // Descripción de qué cambió. Queda registrado en VersionesDocumento para auditoría.
        [Required(ErrorMessage = "El comentario de cambio es obligatorio para identificar qué se modificó.")]
        public string ComentarioCambio { get; set; } = string.Empty;

        // Al menos uno de estos dos debe venir. Si vienen los dos, Archivo tiene prioridad.
        public string? ContenidoTexto { get; set; }
        public IFormFile? Archivo { get; set; }
    }
}