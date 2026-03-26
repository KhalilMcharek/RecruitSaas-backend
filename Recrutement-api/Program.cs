using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.Extensions;
using Recrutement_api.Services.Implementations;
using Recrutement_api.Services.Interfaces;
using Recrutement_api.Services.TenantService;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Créer le dossier logs si absent
// --------------------------
Directory.CreateDirectory("logs");

// --------------------------
// CONFIGURATION SERILOG
// --------------------------
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

// --------------------------
// SERVICES
// --------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddScoped<ILinkGeneratorService, LinkGeneratorService>();
builder.Services.AddScoped<Recrutement_api.Services.IAuthService, Recrutement_api.Services.AuthService>();
builder.Services.AddScoped<Recrutement_api.Services.IJwtService, Recrutement_api.Services.JwtService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IOffreService, Recrutement_api.Services.OffreService>();
builder.Services.AddScoped<Recrutement_api.Services.TenantService.CandidatureService>();
builder.Services.AddScoped<ITeamService, TeamService>();

// --------------------------
// JWT & AUTHORIZATION
// --------------------------
builder.Services.AddJwtAuthentication(builder.Configuration);

// =============================
// CORS
// =============================

// Déplacer AddCors **avant Build**
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // port Vite par exemple
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// =============================
// DATABASE
// =============================



// =============================
// BUILD APP
// =============================

var app = builder.Build();

// =============================
// MIDDLEWARE
// =============================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");


app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint (monitoring simple)
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.MapControllers();


app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls;
    foreach (var url in urls)
    {
        Console.WriteLine($"Application running on: {url}");
    }
});

app.Run();