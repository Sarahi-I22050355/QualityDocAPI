using System;
using System.ComponentModel;
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

        [Required(ErrorMessage = "El ID de la categoría es obligatorio.")]
        [DefaultValue(1)] 
        [Description("OPCIONES: 1=Manual de Calidad, 2=Procedimientos, 3=Instrucciones, 4=Formatos, 5=Registros, 6=Ayudas visuales")]
        public int IdCategoria { get; set; }

        // Ambos son opcionales aquí, la validación ruda la haremos en el Controlador
        public string? ContenidoTexto { get; set; } 
        public IFormFile? Archivo { get; set; } 

        public string[] Etiquetas { get; set; } = Array.Empty<string>();
    }
}