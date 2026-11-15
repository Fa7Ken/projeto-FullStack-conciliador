using Conciliador.Application.Auth;
using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Conciliador.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RunReconciliationRequestValidator>();
        services.AddSingleton<ReconciliationEngine>();
        services.AddScoped<RunReconciliationUseCase>();
        services.AddScoped<ReconciliationQueries>();
        services.AddScoped<LoginUseCase>();
        return services;
    }
}
