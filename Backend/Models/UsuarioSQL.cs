namespace QualityDocAPI.Models
{
    public class UsuarioSQL
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }

        public int? IdArea { get; set; }
        public bool EsAreaGeneral { get; set; }
        public string NombreArea { get; set; } = string.Empty;

        // NULL = super-admin (no pertenece a ninguna empresa)
        public int? IdEmpresa { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;

        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}