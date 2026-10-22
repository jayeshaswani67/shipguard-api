
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using ShipGuard.Api.Services;
using ShipGuard.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB configuration
builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var configuration =
        serviceProvider.GetRequiredService<IConfiguration>();

    var connectionString =
        configuration["MongoDB:ConnectionString"];

    var databaseName =
        configuration["MongoDB:DatabaseName"];

    if (string.IsNullOrWhiteSpace(connectionString) ||
        string.IsNullOrWhiteSpace(databaseName))
    {
        throw new InvalidOperationException(
            "MongoDB connection string or database name is missing.");
    }

    var client = new MongoClient(connectionString);
    return client.GetDatabase(databaseName);
});

// JWT settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>() ?? new JwtSettings();

// The secret must be supplied through Jwt__Secret.
jwtSettings.Secret = builder.Configuration["Jwt:Secret"]
    ?? string.Empty;

if (Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
{
    throw new InvalidOperationException(
        "Configure Jwt__Secret with a secret of at least 32 bytes.");
}

// Authentication service
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<TrivyOptions>(
    builder.Configuration.GetSection("Trivy"));

builder.Services.AddSingleton<TrivyScannerService>();
builder.Services.AddSingleton<GitHubCheckoutService>();

// Validate JWTs on protected endpoints
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Secret)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType =
                    System.Security.Claims.ClaimTypes.Name,

                RoleClaimType =
                    System.Security.Claims.ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();

// CORS for the Vite frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
