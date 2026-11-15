using Conciliador.Domain.Reconciliations;

namespace Conciliador.Application.Reconciliations;

public sealed record ReconciliationLineDto(
    DateOnly Date, string Brand, decimal SalesAmount, decimal StatementAmount, int SalesCount, decimal Difference, LineStatus Status);

public sealed record BrandSubtotalDto(
    string Brand, decimal SalesAmount, decimal StatementAmount, decimal Difference, int MatchedLines, int DivergentLines);

public sealed record ReconciliationIssueDto(IssueSeverity Severity, string Code, string Message);

public sealed record SourceDocumentDto(string FileName, SourceKind Kind, DateOnly? PeriodStart, DateOnly? PeriodEnd, int EntryCount);

public sealed record ReconciliationSummaryDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string CreatedBy,
    OperationType Operation,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalSales,
    decimal TotalStatement,
    decimal TotalDifference,
    int MatchedLines,
    int DivergentLines,
    bool IsFullyMatched);

public sealed record ReconciliationDetailDto(
    ReconciliationSummaryDto Summary,
    IReadOnlyList<BrandSubtotalDto> Brands,
    IReadOnlyList<ReconciliationLineDto> Lines,
    IReadOnlyList<ReconciliationIssueDto> Issues,
    IReadOnlyList<SourceDocumentDto> Sources);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class ReconciliationMappings
{
    public static ReconciliationSummaryDto ToSummary(this Reconciliation r) => new(
        r.Id, r.CreatedAtUtc, r.CreatedBy, r.Operation, r.StartDate, r.EndDate,
        r.TotalSales, r.TotalStatement, r.TotalDifference, r.MatchedLines, r.DivergentLines, r.IsFullyMatched);

    public static ReconciliationDetailDto ToDetail(this Reconciliation r)
    {
        var lines = r.Lines
            .OrderBy(l => l.Date).ThenBy(l => l.Brand, StringComparer.Ordinal)
            .Select(l => new ReconciliationLineDto(l.Date, l.Brand, l.SalesAmount, l.StatementAmount, l.SalesCount, l.Difference, l.Status))
            .ToList();

        var brands = r.Lines
            .GroupBy(l => l.Brand)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BrandSubtotalDto(
                g.Key,
                g.Sum(l => l.SalesAmount),
                g.Sum(l => l.StatementAmount),
                g.Sum(l => l.Difference),
                g.Count(l => l.Status == LineStatus.Matched),
                g.Count(l => l.Status != LineStatus.Matched)))
            .ToList();

        var issues = r.Issues
            .OrderByDescending(i => i.Severity)
            .Select(i => new ReconciliationIssueDto(i.Severity, i.Code, i.Message))
            .ToList();

        var sources = r.Sources
            .OrderBy(s => s.Kind).ThenBy(s => s.PeriodStart).ThenBy(s => s.FileName, StringComparer.Ordinal)
            .Select(s => new SourceDocumentDto(s.FileName, s.Kind, s.PeriodStart, s.PeriodEnd, s.EntryCount))
            .ToList();

        return new ReconciliationDetailDto(r.ToSummary(), brands, lines, issues, sources);
    }
}
