using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Recrutement_api.Configuration;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Extensions;
using Recrutement_api.Services;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.Expert;
using Recrutement_api.Services.Implementations;
using Recrutement_api.Services.Interfaces;
using Recrutement_api.Services.Shared;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.Services.AI;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Email config
builder.Configuration["EmailSettings:SmtpHost"] = "smtp-relay.brevo.com";
builder.Configuration["EmailSettings:SmtpPort"] = "587";
builder.Configuration["EmailSettings:SmtpUseSsl"] = "false";
builder.Configuration["EmailSettings:SmtpUsername"] = "a472ab001@smtp-brevo.com";
builder.Configuration["EmailSettings:SmtpPassword"] = "xsmtpsib-...";
builder.Configuration["EmailSettings:FromEmail"] = "gofranelaradh@gmail.com";
builder.Configuration["EmailSettings:FromName"] = "RecruitSaaS";
builder.Configuration["EmailSettings:FrontendBaseUrl"] = "http://localhost:5173";

// Serilog
Directory.CreateDirectory("logs");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [Tenant:{TenantId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.File("logs/errors-.txt", rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

builder.Host.UseSerilog();

// Services
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// CV Extraction
builder.Services.AddHttpClient(); // ✅ une seule fois
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.Extractors.PdfTextExtractor>();
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.Extractors.DocxTextExtractor>();
builder.Services.AddSingleton<Recrutement_api.Services.CvExtraction.Extractors.OcrExtractor>();
builder.Services.AddSingleton<Recrutement_api.Services.CvExtraction.Extractors.TesseractTextExtractor>();
builder.Services.AddScoped<
    Recrutement_api.Services.CvExtraction.ICvExtractionService,
    Recrutement_api.Services.CvExtraction.CvExtractionService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:8080", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Entrez: Bearer {votre_token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Services métier
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ILinkGeneratorService, LinkGeneratorService>();
builder.Services.AddScoped<IOffreService, OffreService>();
builder.Services.AddScoped<IExpertService, ExpertFonctionsService>();
builder.Services.AddScoped<ICandidatService, CandidatService>();

builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<CandidatureService>();
builder.Services.AddScoped<ITeamService, TeamService>();

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddScoped<Recrutement_api.Services.Expert.ExpertService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<EmailQueueService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailQueueService>());

builder.Services.AddScoped<IExpertService, ExpertUserService>();
builder.Services.AddScoped<CandidateProfileService>();
builder.Services.AddScoped<TenantProfileService>();
builder.Services.AddScoped<IAiOrchestratorService, AiOrchestratorService>();

// Nouveaux services
builder.Services.AddScoped<
    Recrutement_api.Services.Entretien.IEntretienService,
    Recrutement_api.Services.Entretien.EntretienService>();

builder.Services.AddScoped<Recrutement_api.Services.TenantServices.ReportService>();

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 1. CORS
app.UseCors("AllowFrontend");

// 2. Static files (GLB support)
var webRoot = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

if (!Directory.Exists(webRoot))
    Directory.CreateDirectory(webRoot);

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// 3. Auth
app.UseAuthentication();
app.UseAuthorization();

// Health check
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.MapControllers();

app.Run();