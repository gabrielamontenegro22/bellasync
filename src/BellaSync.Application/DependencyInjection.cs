using System.Reflection;
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
        // Auto-registro de todos los AbstractValidator<T> de este assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }
}
