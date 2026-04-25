using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class CrearAreaDTO
    {
        [Required(ErrorMessage = "El nombre del área es obligatorio.")]
        [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        // Solo el Admin puede crear un área General. Por defecto es false.
        public bool EsGeneral { get; set; } = false;
    }
}