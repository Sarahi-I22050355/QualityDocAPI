using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace QualityDocAPI.DTOs
{
    public class NuevoDocumentoDTO
    {
        [Required(ErrorMessage = "El título del documento es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El título es demasiado largo, máximo 100 caracteres.")]
        public string Titulo { get; set; } = string.Empty;

        // El autor ahora es opcional — se toma del token JWT si no se envía
        public string? Autor { get; set; }

        [Required(ErrorMessage = "El ID de la categoría es obligatorio.")]
        [DefaultValue(1)]
        [Description("OPCIONES: 1=Manual de Calidad, 2=Procedimiento, 3=Instrucción de Trabajo, 4=Registro de Calidad, 5=Plan de Control, 6=Auditoría")]
        public int IdCategoria { get; set; }

        // Solo aplica para usuarios del área General.
        // Los demás heredan su área automáticamente del token JWT.
        public int? IdArea { get; set; }

        public string? ContenidoTexto { get; set; }
        public IFormFile? Archivo { get; set; }

        // Etiquetas para búsqueda — palabras clave separadas
        public string[] Etiquetas { get; set; } = Array.Empty<string>();
    }
}