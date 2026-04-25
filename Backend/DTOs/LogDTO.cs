namespace QualityDocAPI.DTOs
{
    public class LogDTO
    {
        public int     IdLog           { get; set; }
        public int     IdUsuario       { get; set; }
        public string  NombreUsuario   { get; set; } = string.Empty;
        public string  AreaUsuario     { get; set; } = string.Empty;  // Nombre del área del usuario
        public int?    IdDocumento     { get; set; }
        public string? TituloDocumento { get; set; }
        public string  Accion          { get; set; } = string.Empty;
        public string? Detalle         { get; set; }
        public string? IpAddress       { get; set; }
        public DateTime FechaAccion    { get; set; }
    }
}