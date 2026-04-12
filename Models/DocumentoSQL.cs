using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace QualityDocAPI.Models
{
    [Table("Documentos")] // Asegura que busque la tabla correcta
    public class DocumentoSQL
    {
        [Key]
        [Column("id_documento")]
        public int Id { get; set; }

        [Column("id_usuario")]
        public int IdUsuario { get; set; } 

        [Column("id_categoria")]
        public int IdCategoria { get; set; }

        [Column("id_estado")]
        public int IdEstado { get; set; }

        [Column("titulo")]
        public string Titulo { get; set; } = string.Empty;

        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("ruta_archivo")]
        public string RutaArchivo { get; set; } = string.Empty;

        [Column("nombre_archivo")]
        public string NombreArchivo { get; set; } = string.Empty;

        [Column("extension")]
        public string Extension { get; set; } = "pdf";

        [Column("numero_version")]
        public short NumeroVersion { get; set; } = 1;

        [Column("fecha_subida")]
        public DateTime FechaSubida { get; set; } = DateTime.Now;

        [Column("fecha_modificacion")]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;
    }
}