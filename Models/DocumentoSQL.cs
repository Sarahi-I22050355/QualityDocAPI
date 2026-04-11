namespace QualityDocAPI.Models
{
    public class DocumentoSQL
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Autor { get; set; } = string.Empty;
        public string Estado { get; set; } = "Borrador"; // Borrador, Revisión, Aprobado
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}