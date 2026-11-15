using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Conciliador.Infrastructure.Persistence;

// Usada apenas pelo "dotnet ef" para gerar migrations; nao conecta em banco real
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=conciliador;Username=postgres;Password=design-time-only")
            .Options;

        return new AppDbContext(options);
    }
}
