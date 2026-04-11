namespace QualityDocAPI.Models
{
    public class DocumentoMongo
    {
        public string Id { get; set; } = string.Empty; // MongoDB usa un ID alfanumérico
        public int SqlId { get; set; } // La referencia para saber de qué documento en SQL hablamos
        public string Titulo { get; set; } = string.Empty;
        public string ContenidoTexto { get; set; } = string.Empty; // Aquí guardaremos todo el texto del PDF
        public string[] Etiquetas { get; set; } = Array.Empty<string>(); // Palabras clave para la búsqueda rápida
    }
}