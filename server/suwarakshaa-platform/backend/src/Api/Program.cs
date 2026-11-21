using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Api.Background;
using Api.Services;
using Application.Abstractions;
using Application.Services;
using Application.Strategies;
using Infrastructure.External;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polly;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    { Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {{ new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }});
});

// CORS for the React client
builder.Services.AddCors(o => o.AddPolicy("client", p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod()));

// Database + DI
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Default")));
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<AiGovernanceService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<MetricsService>();
builder.Services.AddSingleton<SensitiveDataProtector>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(cfg["DataProtection:KeysPath"] ?? "keys")).SetApplicationName("ReliefLink");
builder.Services.AddSingleton<IPriorityStrategy, WeightedPriorityStrategy>();
builder.Services.AddSingleton<IAllocationStrategy, MultiConstraintAllocationStrategy>();

// AI service client: timeout + retry + circuit breaker (fallback is inside AiClient)
builder.Services.AddHttpClient<IAiClient, AiClient>(c =>
{
    c.BaseAddress = new Uri(cfg["AiService:BaseUrl"] ?? "http://localhost:5100");
    c.Timeout = TimeSpan.FromSeconds(5);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, n => TimeSpan.FromMilliseconds(300 * n)))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Async event processing (transactional outbox)
builder.Services.AddSingleton<INotificationSenderFactory, NotificationSenderFactory>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<NotificationDispatcher>();
builder.Services.AddHostedService<KpiSnapshotService>();

// Routing (OSRM) and the AI assistant/summary/translation client, both with timeouts and retries
builder.Services.AddHttpClient<IRoutingService, OsrmRoutingService>(c => { c.BaseAddress = new Uri("https://router.project-osrm.org/"); c.Timeout = TimeSpan.FromSeconds(5); })
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(1, _ => TimeSpan.FromMilliseconds(300)));
builder.Services.AddHttpClient<IAiAssistantClient, AiAssistantClient>(c => { c.BaseAddress = new Uri(cfg["AiService:BaseUrl"] ?? "http://localhost:5100"); c.Timeout = TimeSpan.FromSeconds(10); })
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(1, _ => TimeSpan.FromMilliseconds(300)))
    .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Map integration (OpenStreetMap Nominatim) with timeout; failures return null and the app carries on
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(c =>
{
    c.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ReliefLink-SE205-coursework/1.0");
    c.Timeout = TimeSpan.FromSeconds(5);
});

// JWT + RBAC (short claim names: sub, role)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = cfg["Jwt:Issuer"], ValidAudience = cfg["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
        RoleClaimType = "role", NameClaimType = "name", ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();
static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("default",   c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("assistant", c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("auth",      c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 6 * 1024 * 1024);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try { await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), log); }
    catch (Exception ex) { log.LogError(ex, "Seeding failed. Is the database created? Run db/HumanitarianPlatform_Schema.sql."); }
}

// never leak stack traces: a generic problem response with a trace id that can be looked up in the logs
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred.", traceId = ctx.TraceIdentifier });
}));
if (cfg.GetValue<bool>("Security:RequireHttps")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
app.UseSwagger(); app.UseSwaggerUI();
app.UseCors("client");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok("ok"));
app.MapControllers().RequireRateLimiting("default");
app.Run();
