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


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);



// Services d'extraction de CV
builder.Services.AddHttpClient(); // Requis par CvParserService
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.Extractors.PdfTextExtractor>();
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.Extractors.DocxTextExtractor>();
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.Extractors.OcrExtractor>();
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.ICvExtractionService, Recrutement_api.Services.CvExtraction.CvExtractionService>();
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

// Tes services
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<AdminService>();


builder.Services.AddScoped<ILinkGeneratorService, LinkGeneratorService>();


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

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();


builder.Services.AddHttpContextAccessor();

// =============================

builder.Services.AddScoped<IOffreService, Recrutement_api.Services.OffreService>();

builder.Services.AddScoped<Recrutement_api.Services.CandidatServices.ICandidatService, Recrutement_api.Services.CandidatServices.CandidatService>();

builder.Services.AddScoped<CandidatureService>();
builder.Services.AddScoped<ITeamService, TeamService>();
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






if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();




app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = ""
});


app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.MapControllers();


app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var url in app.Urls)
    {
        Console.WriteLine($"Application running on: {url}");
    }
});

app.Run();