using Conciliador.Application.Abstractions;
using Conciliador.Application.Common;

namespace Conciliador.Application.Reconciliations;

public sealed class ReconciliationQueries
{
    public const int MaxPageSize = 100;

    private readonly IReconciliationRepository _repository;

    public ReconciliationQueries(IReconciliationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ReconciliationDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var reconciliation = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Conciliação não encontrada.");

        return reconciliation.ToDetail();
    }

    public async Task<PagedResult<ReconciliationSummaryDto>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await _repository.ListSummariesAsync(page, pageSize, cancellationToken);
        return new PagedResult<ReconciliationSummaryDto>(items, page, pageSize, total);
    }
}
