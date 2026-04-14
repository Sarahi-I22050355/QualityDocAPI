using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using QualityDocAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Agrega los controladores
builder.Services.AddControllers();

// --- 1. CONFIGURAR SQL SERVER ---
var sqlConnectionString = builder.Configuration.GetConnectionString("SqlConexion");
builder.Services.AddDbContext<SqlContext>(options =>
    options.UseSqlServer(sqlConnectionString));

// --- 2. CONFIGURAR MONGODB ---
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoConexion");
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

// --- 3. CONFIGURAR SWAGGER CON BOTÓN DE SEGURIDAD ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "QualityDoc API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Escribe 'Bearer' [espacio] y luego tu token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// --- 4. CONFIGURAR CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("PaseVIP", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- 5. CONFIGURACIÓN DEL GUARDIA (AUTENTICACIÓN JWT) ---
var jwtKey = builder.Configuration["Jwt:Key"];
var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// --- 6. DEFINIR REGLAS DE ROLES (AUTORIZACIÓN) ---
builder.Services.AddAuthorization(options =>
{
    // --- CORRECCIÓN: "idRol" debe ser exactamente igual a como se guardó en el Token (minúscula) ---
    // Aquí definimos que el rol 1 es el Admin
    options.AddPolicy("SoloAdmin", policy => policy.RequireClaim("idRol", "1"));
    // Nueva política: Deja pasar si eres Admin (1) O Supervisor (2)
    options.AddPolicy("SubeYAprueba", policy => policy.RequireClaim("idRol", "1", "2"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- EL ORDEN DE ESTAS LÍNEAS ES VITAL ---
app.UseCors("PaseVIP");      // 1. Deja entrar al frontend
app.UseAuthentication();     // 2. Revisa el gafete (Token)
app.UseAuthorization();      // 3. Revisa si tienes permiso para esa zona específica

app.MapControllers();
app.Run();