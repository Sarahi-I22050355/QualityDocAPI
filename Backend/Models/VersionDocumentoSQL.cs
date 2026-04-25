using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QualityDocAPI.Models
{
    [Table("VersionesDocumento")]
    public class VersionDocumentoSQL
    {
        [Key]
        [Column("id_version")]
        public int Id { get; set; }

        // Documento al que pertenece esta versión
        [Column("id_documento")]
        public int IdDocumento { get; set; }

        // Quién subió esta versión
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        // 1 para la versión inicial, 2 para la siguiente, etc.
        [Column("numero_version")]
        public short NumeroVersion { get; set; }

        // Ruta en el servidor Linux (via SFTP). "Sin archivo físico" si es documento de texto.
        [Column("ruta_archivo")]
        public string RutaArchivo { get; set; } = string.Empty;

        [Column("nombre_archivo")]
        public string NombreArchivo { get; set; } = string.Empty;

        [Column("tamano_bytes")]
        public long? TamanoBytes { get; set; }

        // Descripción de qué cambió en esta versión. Obligatorio al subir versiones >= 2.
        [Column("comentario_cambio")]
        public string? ComentarioCambio { get; set; }

        [Column("fecha_version")]
        public DateTime FechaVersion { get; set; } = DateTime.Now;
    }
}