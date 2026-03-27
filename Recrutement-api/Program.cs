using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Extensions;
using Recrutement_api.Services;
using Recrutement_api.Services.Implementations;
using Recrutement_api.Services.Interfaces;


using Serilog;
using Serilog.Events;
using Recrutement_api.Services.TenantServices;

var builder = WebApplication.CreateBuilder(args);

// =============================
// DOSSIER LOGS
// =============================
Directory.CreateDirectory("logs");

// =============================
// CONFIGURATION SERILOG
// =============================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
        "{Timestamp:HH:mm:ss} [{Level:u3}] [Tenant:{TenantId}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [Tenant:{TenantId}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "logs/errors-.txt",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Error,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [Tenant:{TenantId}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

<<<<<<< HEAD
// --------------------------
// SERVICES
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

=======
// =============================
// DATABASE
// =============================
>>>>>>> main
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// =============================
// SERVICES
// =============================

// Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Tes services
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<AdminService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddScoped<ILinkGeneratorService, LinkGeneratorService>();
<<<<<<< HEAD

// --------------------------
// CORS
// --------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:8080",
                "http://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --------------------------
// JWT & AUTHENTICATION — une seule fois via l'extension
// --------------------------
builder.Services.AddJwtAuthentication(builder.Configuration);

// --------------------------
// SERVICES MÉTIER
// --------------------------
builder.Services.AddScoped<Recrutement_api.Services.IAuthService, Recrutement_api.Services.AuthService>();
builder.Services.AddScoped<Recrutement_api.Services.IJwtService, Recrutement_api.Services.JwtService>();
builder.Services.AddHttpContextAccessor();
=======
>>>>>>> main
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<IOffreService, OffreService>();

builder.Services.AddHttpContextAccessor();

// =============================

builder.Services.AddScoped<IOffreService, Recrutement_api.Services.OffreService>();
<<<<<<< HEAD


builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));
// --------------------------
// CONTROLLERS
// --------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// --------------------------
// BUILD APP
// --------------------------
var app = builder.Build();

=======
builder.Services.AddScoped<CandidatureService>();
builder.Services.AddScoped<ITeamService, TeamService>();


builder.Services.AddJwtAuthentication(builder.Configuration);

// =============================
// CORS
// =============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// =============================
// BUILD
// =============================
var app = builder.Build();

// =============================
// MIDDLEWARE
// =============================
>>>>>>> main
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

<<<<<<< HEAD
// CORS — avant tout le reste
=======
app.UseHttpsRedirection();

>>>>>>> main
app.UseCors("AllowFrontend");

// ⚠️ IMPORTANT : middleware tenant AVANT auth


app.UseAuthentication();
app.UseAuthorization();

<<<<<<< HEAD
=======
// =============================
// STATIC FILES
// =============================
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = ""
});

// =============================
// HEALTH CHECK
// =============================
>>>>>>> main
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.MapControllers();

// Log URLs
app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"Application running on: {url}");
    }
});

app.Run();