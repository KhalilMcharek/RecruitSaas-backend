using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Recrutement_api.Data;
using Recrutement_api.Services;
using Recrutement_api.Extensions;
using Recrutement_api.Middleware;
using Serilog;
using Serilog.Events;

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

// =============================
// DATABASE
// =============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// =============================
// SERVICES
// =============================
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<AdminService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =============================
// JWT AUTHENTICATION
// =============================
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

// Tenant middleware AVANT auth
app.UseMiddleware<TenantLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

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
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.MapControllers();

app.Run();