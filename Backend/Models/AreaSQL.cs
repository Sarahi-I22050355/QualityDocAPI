using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QualityDocAPI.Models
{
    [Table("Areas")]
    public class AreaSQL
    {
        [Key]
        [Column("id_area")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        // Si es true, los usuarios de esta área ven TODOS los documentos sin restricción
        [Column("es_general")]
        public bool EsGeneral { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}