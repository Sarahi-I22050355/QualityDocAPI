using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QualityDocAPI.Models
{
    [Table("LogAccesos")]
    public class LogAccesoSQL
    {
        [Key]
        [Column("id_log")]
        public int Id { get; set; }

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("id_documento")]
        public int? IdDocumento { get; set; }

        [Column("accion")]
        public string Accion { get; set; } = string.Empty;

        [Column("detalle")]
        public string? Detalle { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("fecha_accion")]
        public DateTime FechaAccion { get; set; }
    }
}