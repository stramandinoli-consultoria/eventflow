using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace EventFlow.Api.IntegrationTests;

/// <summary>
/// Sobe a API inteira em memória contra um banco PostgreSQL de teste (eventflow_tests),
/// recriado do zero a cada execução, com as migrations e os dados de exemplo.
/// Conexão: variável EVENTFLOW_TEST_DB ou o PostgreSQL local padrão.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private const string DatabaseName = "eventflow_tests";
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable("EVENTFLOW_TEST_DB")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

        await using (var admin = new NpgsqlConnection(baseConnection + ";Database=postgres"))
        {
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }

        // Variáveis de ambiente valem já na montagem dos serviços do Program.cs.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"{baseConnection};Database={DatabaseName}");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("Database__Seed", "true");
        Environment.SetEnvironmentVariable("Webhooks__RetryWorkerEnabled", "false");
        Environment.SetEnvironmentVariable("Webhooks__AllowPrivateNetworks", "true");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPerMinute", "10000");

        Factory = new WebApplicationFactory<Program>();
        _ = Factory.Server; // força a inicialização (migrations + seed)
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    public HttpClient Client(string? token = null)
    {
        var client = Factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var response = await Client().PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>(Json))!;
    }

    public Task<AuthResult> AdminAsync() => LoginAsync("admin@eventflow.app", "admin123");
    public Task<AuthResult> PartnerAsync() => LoginAsync("burger@demo.com", "demo123");
    public Task<AuthResult> CustomerAsync() => LoginAsync("cliente@demo.com", "demo123");
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

public sealed record UserResult(string Id, string Name, string Email, string? Phone, string Role, string? PartnerId);
public sealed record AuthResult(string AccessToken, string RefreshToken, UserResult User);
public sealed record ItemResult(string ProductId, string ProductName, int Qty, decimal UnitPrice);
public sealed record TimelineResult(string Status, DateTimeOffset At, string? Note);
public sealed record OrderResult(string Id, string EventId, string StandId, string StandName, string? CustomerId,
    string CustomerName, string CustomerPhone, List<ItemResult> Items, decimal Total, string Status,
    List<TimelineResult> Timeline, DateTimeOffset CreatedAt);
public sealed record PlacedResult(OrderResult Order, string AccessKey);
public sealed record PartnerResult(string Id, string CompanyName, string Cnpj, string Email, string StandId);
