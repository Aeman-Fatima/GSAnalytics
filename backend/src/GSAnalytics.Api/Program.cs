using System.Text;
using GSAnalytics.Api.Services;
using GSAnalytics.Application.Auth;
using GSAnalytics.Application.Security;
using GSAnalytics.Infrastructure.Persistence;
using GSAnalytics.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<GSAnalyticsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GSAnalytics")));

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, GSAnalytics.Infrastructure.Auth.AuthService>();
builder.Services.AddScoped<GSAnalytics.Application.Imports.ICsvImportService, GSAnalytics.Infrastructure.Imports.CsvImportService>();
builder.Services.AddScoped<GSAnalytics.Application.Insights.IInsightsService, GSAnalytics.Infrastructure.Insights.InsightsService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types exactly as issued ("sub", "businessId") instead of ASP.NET's default
        // remap to long ClaimTypes.* URIs — CurrentUserService reads the raw JWT claim names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// A crude but effective brake on credential-stuffing / registration-spam against the unauthenticated
// auth endpoints — applied per client IP, not per account, since there's no account yet to key on.
// The permit count is configurable so Development/tests (many rapid requests from one "client") can
// raise it well above the production default without changing any code.
var authRateLimitPermits = builder.Configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = authRateLimitPermits;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS: the frontend is a separate static site (served independently, e.g. `npx serve .`),
// so its dev origin(s) must be explicitly allowlisted here rather than using AllowAnyOrigin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
const string FrontendCorsPolicy = "FrontendCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GS Analytics API",
        Version = "v1",
        Description = "Backend API for the GS Analytics sales intelligence application."
    });

    // Lets Swagger UI's "Authorize" button attach a bearer token to requests for manual testing.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GS Analytics API v1");
    });

    // Convenience for local development only: apply pending migrations and seed demo data
    // automatically so `dotnet run` gives you a fully working app. Production deployments
    // should apply migrations as an explicit step instead of on every app startup.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GSAnalyticsDbContext>();
    await db.Database.MigrateAsync();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DemoDataSeeder.SeedAsync(db, passwordHasher);
}
else
{
    // Never leak stack traces / internal exception details outside Development.
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"message":"An unexpected error occurred."}""");
        });
    });
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the implicit Program class to GSAnalytics.Tests for WebApplicationFactory<Program>.
public partial class Program { }
