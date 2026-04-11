using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.DTOs
{
    public class NuevoDocumentoDTO
    {
        [Required(ErrorMessage = "¡Hey! El título del documento es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El título es demasiado largo, máximo 100 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Necesitamos saber quién es el autor.")]
        public string Autor { get; set; } = string.Empty;

        [Required(ErrorMessage = "El documento no puede estar vacío, agrega el contenido.")]
        public string ContenidoTexto { get; set; } = string.Empty;

        // Las etiquetas son opcionales, así que no le ponemos [Required]
        public string[] Etiquetas { get; set; } = Array.Empty<string>();
    }
}