using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using QualityDocAPI.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// --- 1. SQL SERVER ---
var sqlConnectionString = builder.Configuration.GetConnectionString("SqlConexion");
builder.Services.AddDbContext<SqlContext>(options =>
    options.UseSqlServer(sqlConnectionString));

// --- 2. MONGODB ---
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoConexion");
builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

// --- 3. SWAGGER ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "QualityDoc API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
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

// --- 4. CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("PaseVIP", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- 5. JWT ---
var jwtKey  = builder.Configuration["Jwt:Key"];
var keyBytes = Encoding.UTF8.GetBytes(jwtKey!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken            = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer           = false,
            ValidateAudience         = false
        };
    });

// --- 6. POLÍTICAS DE ROL ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdmin",    policy => policy.RequireClaim("idRol", "1"));
    options.AddPolicy("SubeYAprueba", policy => policy.RequireClaim("idRol", "1", "2"));
    options.AddPolicy("PuedeRevisar", policy => policy.RequireClaim("idRol", "1", "4"));
    options.AddPolicy("EsSuperAdmin", policy => policy.RequireClaim("idRol", "5"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("PaseVIP");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();