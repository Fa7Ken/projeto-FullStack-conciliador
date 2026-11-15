using Conciliador.Application.Abstractions;
using Conciliador.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conciliador.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminName { get; set; } = "Administrador";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

// Aplica as migrations e cria o usuario inicial para o projeto rodar com login pronto
public sealed class DatabaseInitializer
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly SeedOptions _seed;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppDbContext db, IPasswordHasher hasher, IOptions<SeedOptions> seed, ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _hasher = hasher;
        _seed = seed.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_db.Database.IsNpgsql())
            await _db.Database.MigrateAsync(cancellationToken);
        else
            await _db.Database.EnsureCreatedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_seed.AdminEmail) || string.IsNullOrWhiteSpace(_seed.AdminPassword))
        {
            _logger.LogWarning("Seed admin user skipped: Seed:AdminEmail or Seed:AdminPassword not configured");
            return;
        }

        var email = _seed.AdminEmail.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken)) return;

        _db.Users.Add(new User(_seed.AdminName, email, _hasher.Hash(_seed.AdminPassword)));
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seed admin user created: {Email}", email);
    }
}
