using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Recrutement_api.Configuration;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Extensions;
using Recrutement_api.Services;
using Recrutement_api.Services.CandidatServices;
using Recrutement_api.Services.CandidateServices;
using Recrutement_api.Services.Expert;
using Recrutement_api.Services.Expert.Implementations;
using Recrutement_api.Services.Implementations;
using Recrutement_api.Services.Interfaces;
using Recrutement_api.Services.Shared;
using Recrutement_api.Services.TenantServices;
using Recrutement_api.Services.AI;

var builder = WebApplication.CreateBuilder(args);

// Email config
builder.Configuration["EmailSettings:SmtpHost"] = "smtp-relay.brevo.com";
builder.Configuration["EmailSettings:SmtpPort"] = "587";
builder.Configuration["EmailSettings:SmtpUseSsl"] = "false";
builder.Configuration["EmailSettings:SmtpUsername"] = "a89322001@smtp-brevo.com";
builder.Configuration["EmailSettings:SmtpPassword"] = "xsmtpsib-f51c962ba5c5c8f58dd1f373d73ec9b7503cce2605b9b060534629481b315611-cHSGiZH61fwXWVEn";
builder.Configuration["EmailSettings:FromEmail"] = "oumaymametoui3@gmail.com";
builder.Configuration["EmailSettings:FromName"] = "RecruitSaaS";
builder.Configuration["EmailSettings:FrontendBaseUrl"] = "http://localhost:5173";

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
builder.Services.AddScoped<Recrutement_api.Services.CvExtraction.ICvExtractionService, Recrutement_api.Services.CvExtraction.CvExtractionService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            "http://localhost:5173",  // Vite par défaut
            "http://localhost:8080",  // Vue CLI
            "http://localhost:3000",  // React
            "https://localhost:5173"  // Si vous utilisez HTTPS en frontend
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()  // ✅ CRITIQUE : requis pour les tokens JWT
    );
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
builder.Services.AddScoped<ISavedJobService, SavedJobService>();
builder.Services.AddScoped<NotificationService>();

builder.Services.AddScoped<Recrutement_api.Services.Entretien.IEntretienService, Recrutement_api.Services.Entretien.EntretienService>();
builder.Services.AddScoped<Recrutement_api.Services.Quiz.IQuizService, Recrutement_api.Services.Quiz.QuizService>();

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
    ContentTypeProvider = provider,
    OnPrepareResponse = ctx =>
    {
        // Images servies depuis wwwroot : CORS pour lecture canvas côté Vite (crossOrigin anonymous).
        var path = ctx.File.Name?.ToLowerInvariant() ?? "";
        if (!path.EndsWith(".jpg") && !path.EndsWith(".jpeg") && !path.EndsWith(".png") && !path.EndsWith(".webp") && !path.EndsWith(".jfif"))
            return;
        var origin = ctx.Context.Request.Headers.Origin.ToString();
        if (origin is "http://localhost:5173" or "http://localhost:8080" or "http://localhost:3000")
            ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = origin;
    }
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