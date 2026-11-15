using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using Conciliador.Domain.Users;

namespace Conciliador.Application.Abstractions;

public interface ISalesReportParser
{
    SalesReport Parse(Stream content, string fileName);
}

public interface IBankStatementParser
{
    BankStatement Parse(Stream content, string fileName);
}

public interface IReconciliationRepository
{
    Task AddAsync(Reconciliation reconciliation, CancellationToken cancellationToken);
    Task<Reconciliation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ReconciliationSummaryDto> Items, int Total)> ListSummariesAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateToken(User user);
}

public interface IClock
{
    DateTime UtcNow { get; }
}
