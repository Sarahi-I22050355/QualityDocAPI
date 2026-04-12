using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; 

namespace QualityDocAPI.DTOs
{
    public class NuevoDocumentoDTO
    {
        [Required(ErrorMessage = "¡Hey! El título del documento es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El título es demasiado largo, máximo 100 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Necesitamos saber quién es el autor.")]
        public string Autor { get; set; } = string.Empty;

        // Ambos son opcionales aquí, la validación ruda la haremos en el Controlador
        public string? ContenidoTexto { get; set; } 
        public IFormFile? Archivo { get; set; } 

        public string[] Etiquetas { get; set; } = Array.Empty<string>();
    }
}