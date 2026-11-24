using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EventFlow.Api.Endpoints;
using EventFlow.Api.Infrastructure;
using EventFlow.Api.Mcp;
using EventFlow.Api.Realtime;
using EventFlow.Application;
using EventFlow.Application.Abstractions;
using EventFlow.Infrastructure;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Camadas ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();

// ---------- JSON: enums em snake_case ("awaiting_payment"), igual ao front ----------
var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(enumConverter));

// ---------- Autenticação JWT ----------
var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey(),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // O SignalR (WebSocket) envia o token pela query string.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// ---------- Limite de tentativas no login/cadastro (contra força bruta) ----------
var authPerMinute = config.GetValue("RateLimiting:AuthPerMinute", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- CORS (só necessário quando o front não usa o proxy do Vite) ----------
var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Location")));

// ---------- Tempo real, documentação, erros e MCP ----------
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(enumConverter));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMcpServer(o =>
    {
        o.ServerInfo = new() { Name = "eventflow", Title = "EventFlow", Version = "1.0.0" };
        o.ServerInstructions = "Ferramentas do EventFlow. Use `list_orders` (paginado, ordenado por data) e `get_order` para consultar pedidos com timeline; `update_order_status` ou `bulk_update_order_status` para mudar status (a timeline é registrada automaticamente); `subscribe_order_updates` para receber webhooks assinados com HMAC; `list_webhook_deliveries` e `retry_webhook_deliveries` para auditar e reenviar entregas. Documentação completa em /docs/mcp.";
    })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<EventFlowTools>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || config.GetValue("Docs:Enabled", false))
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("EventFlow API"));
}

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503))
    .WithTags("Saúde").ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapOrderEndpoints();
app.MapHub<OrdersHub>(OrdersHub.Path);
app.MapMcp("/mcp").RequireAuthorization();

// ---------- Banco: aplica migrations e dados de exemplo ----------
if (config.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    if (config.GetValue("Database:Seed", false))
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
