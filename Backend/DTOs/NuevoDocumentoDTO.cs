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
        [Description("OPCIONES: 1=Manual de Calidad, 2=Procedimiento, 3=Instrucción de Trabajo, 4=Registro de Calidad, 5=Plan de Control, 6=Auditoría")]
        public int IdCategoria { get; set; }

        // Solo lo usa el Admin para subir documentos a un área específica.
        // Los Supervisores ignoran este campo: su área siempre viene del token JWT.
        public int? IdArea { get; set; }

        public string? ContenidoTexto { get; set; }
        public IFormFile? Archivo { get; set; }

        public string[] Etiquetas { get; set; } = Array.Empty<string>();
    }
}