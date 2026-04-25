namespace QualityDocAPI.Models
{
    public class UsuarioSQL
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }

        // Área asignada al usuario
        public int? IdArea { get; set; }

        // Calculado en el login: indica si su área tiene visibilidad total
        public bool EsAreaGeneral { get; set; }

        // Nombre del área, se mete como claim en el JWT para usarlo en Mongo sin DB extra
        public string NombreArea { get; set; } = string.Empty;

        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}