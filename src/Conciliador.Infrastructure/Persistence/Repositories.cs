using Conciliador.Application.Abstractions;
using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using Conciliador.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Conciliador.Infrastructure.Persistence;

public sealed class ReconciliationRepository : IReconciliationRepository
{
    private readonly AppDbContext _db;

    public ReconciliationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Reconciliation reconciliation, CancellationToken cancellationToken)
    {
        _db.Reconciliations.Add(reconciliation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<Reconciliation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Reconciliations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<ReconciliationSummaryDto> Items, int Total)> ListSummariesAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await _db.Reconciliations.CountAsync(cancellationToken);

        // Projecao direta: a listagem nao precisa carregar as linhas de cada conciliacao
        var items = await _db.Reconciliations
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReconciliationSummaryDto(
                r.Id, r.CreatedAtUtc, r.CreatedBy, r.Operation, r.StartDate, r.EndDate,
                r.TotalSales, r.TotalStatement, r.TotalDifference, r.MatchedLines, r.DivergentLines,
                r.MatchedLines > 0 && r.DivergentLines == 0))
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}
