using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using LiMS.Api.Data;
using LiMS.Api.Middleware;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("LiMS")
    ?? throw new InvalidOperationException("Connection string 'LiMS' is not configured.");
builder.Services.AddDbContext<LiMSDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__LiMSMigrationsHistory")));
builder.Services.AddDbContext<LegacyLiMSDbContext>(options =>
    options.UseSqlServer(connectionString));

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var generatedDevelopmentKey = false;
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Configure Jwt:SigningKey outside Development.");

    jwtSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    generatedDevelopmentKey = true;
}

var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "LiMS.Api",
    Audience = builder.Configuration["Jwt:Audience"] ?? "LiMS.Clients",
    SigningKey = jwtSigningKey,
    AccessTokenMinutes = builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 120)
};
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes.");

builder.Services.Configure<JwtOptions>(options =>
{
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.SigningKey = jwtOptions.SigningKey;
    options.AccessTokenMinutes = jwtOptions.AccessTokenMinutes;
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IReaderService, ReaderService>();
builder.Services.AddScoped<IReservationQueueService, ReservationQueueService>();
builder.Services.AddScoped<ILibraryQueryService, LibraryQueryService>();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500")
          .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (generatedDevelopmentKey)
    app.Logger.LogWarning("No JWT signing key configured. A temporary development key was generated; existing tokens will expire when the API restarts.");

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await DatabaseInitializer.InitializeAsync(app.Services, builder.Configuration, app.Logger);
app.Run();
