using Conciliador.Application.Abstractions;
using Conciliador.Infrastructure.Persistence;
using Conciliador.Infrastructure.Security;
using Conciliador.Infrastructure.Spreadsheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conciliador.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISalesReportParser, SalesReportParser>();
        services.AddSingleton<IBankStatementParser, BankStatementParser>();

        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
