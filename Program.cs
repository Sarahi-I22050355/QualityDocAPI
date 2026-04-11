using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using QualityDocAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Agrega los controladores (la puerta de Alejandro)
builder.Services.AddControllers();

// --- 1. CONFIGURAR SQL SERVER ---
var sqlConnectionString = builder.Configuration.GetConnectionString("SqlConexion");
builder.Services.AddDbContext<SqlContext>(options =>
    options.UseSqlServer(sqlConnectionString));

// --- 2. CONFIGURAR MONGODB ---
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoConexion");
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

// Configuración para documentar tu API (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- 3. CONFIGURAR CORS (El Pase VIP para Alejandro) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("PaseVIP", policy =>
    {
        policy.AllowAnyOrigin()  // Permite que cualquier frontend se conecte
              .AllowAnyHeader()  // Permite cualquier tipo de dato (JSON, texto, etc.)
              .AllowAnyMethod(); // Permite los botones GET, POST, PUT, DELETE
    });
});

var app = builder.Build();

// Configurar el panel de pruebas visual de tu API
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- EL ORDEN CORRECTO DE LA TUBERÍA (PIPELINE) ---

// 1. Primero el CORS (El cadenero deja pasar a Alejandro)
app.UseCors("PaseVIP");

// 2. Luego la Autorización (Revisa credenciales si las hay)
app.UseAuthorization();

// 3. Al final llegan a las rutas (Tus controladores)
app.MapControllers();

// ¡Arranca el motor!
app.Run();