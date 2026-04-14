namespace QualityDocAPI.Models
{
    public class UsuarioSQL
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}