using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class CambiarEstadoUsuarioDTO
    {
        [Required(ErrorMessage = "El campo Activo es obligatorio.")]
        public bool Activo { get; set; }
    }
}