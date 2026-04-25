using Microsoft.EntityFrameworkCore;
using QualityDocAPI.Models;

namespace QualityDocAPI.Data
{
    public class SqlContext : DbContext
    {
        public SqlContext(DbContextOptions<SqlContext> options) : base(options) { }

        // Tablas originales
        public DbSet<DocumentoSQL>   Documentos   { get; set; }
        public DbSet<AreaSQL>        Areas        { get; set; }
        public DbSet<LogAccesoSQL>   LogAccesos   { get; set; }

        // Nuevas tablas integradas
        public DbSet<FlujoAprobacionSQL>  FlujoAprobacion   { get; set; }
        public DbSet<VersionDocumentoSQL> VersionesDocumento { get; set; }
    }
}