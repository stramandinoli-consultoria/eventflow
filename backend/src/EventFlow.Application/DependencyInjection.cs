using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth;
using EventFlow.Application.Features.Orders;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<AccessScope>();
        services.AddScoped<AuditTrail>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<OrderStatusEffects>();
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Registra todos os handlers (IRequestHandler<,>) do assembly.
        var handlerInterface = typeof(IRequestHandler<,>);
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
                services.AddScoped(iface, type);

        return services;
    }
}
