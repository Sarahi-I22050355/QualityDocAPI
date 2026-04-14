using Microsoft.EntityFrameworkCore;
using QualityDocAPI.Models;

namespace QualityDocAPI.Data
{
    public class SqlContext : DbContext
    {
        // Constructor que recibe la configuración de conexión
        public SqlContext(DbContextOptions<SqlContext> options) : base(options) { }

        // Aquí le decimos que tu modelo DocumentoSQL será una tabla en la base de datos
        public DbSet<DocumentoSQL> Documentos { get; set; }
    }
}