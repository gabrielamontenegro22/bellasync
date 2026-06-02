using System.Reflection;
using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Auth.Shared;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BellaSync.Application;

/// <summary>
/// Punto único de registro de servicios de la capa Application en el contenedor DI.
/// La capa WebApi llama a AddApplication() en Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Auto-registro de todos los AbstractValidator<T> del assembly.
        services.AddValidatorsFromAssembly(assembly);

        // Auto-registro de todos los handlers (ICommandHandler / IQueryHandler).
        // Reflection sobre el assembly buscando clases concretas que implementen
        // las 3 variantes de la interfaz. Cada handler se registra como scoped
        // (consistente con DbContext).
        RegisterHandlers(services, assembly);

        // Servicios compartidos entre handlers de Auth (centralizan lógica
        // de emisión de tokens para evitar duplicación entre Register, Login,
        // RefreshAccessToken).
        services.AddScoped<AuthTokenIssuer>();

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaces = new[]
        {
            typeof(ICommandHandler<,>),
            typeof(ICommandHandler<>),
            typeof(IQueryHandler<,>),
        };

        foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                if (handlerInterfaces.Contains(iface.GetGenericTypeDefinition()))
                {
                    services.AddScoped(iface, type);
                }
            }
        }
    }
}
