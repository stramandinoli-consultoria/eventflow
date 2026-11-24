using EventFlow.Application.Abstractions;
using EventFlow.Infrastructure.Notifications;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Security;
using EventFlow.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Default (PostgreSQL).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderIdGenerator, OrderIdGenerator>();
        services.AddScoped<DbSeeder>();

        services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key precisa ter pelo menos 32 caracteres.")
            .ValidateOnStart();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ICrypto, Crypto>();
        services.AddSingleton(TimeProvider.System);

        var allowPrivateNetworks = config.GetValue("Webhooks:AllowPrivateNetworks", false);
        services.AddHttpClient(WebhookDispatcher.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(8))
            .ConfigurePrimaryHttpMessageHandler(() => SafeConnect.CreateHandler(allowPrivateNetworks));
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        if (config.GetValue("Webhooks:RetryWorkerEnabled", true)) services.AddHostedService<WebhookRetryWorker>();

        services.AddSingleton<IWhatsAppSender, LoggingWhatsAppSender>();
        return services;
    }
}
