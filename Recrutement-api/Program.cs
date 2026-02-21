using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configuration de la base de données PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 🔴 AJOUT : Configuration CORS pour permettre au frontend d'accéder à l'API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // URL du frontend Vite
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 🔴 AJOUT : Activer CORS (doit être avant UseAuthorization et MapControllers)
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();