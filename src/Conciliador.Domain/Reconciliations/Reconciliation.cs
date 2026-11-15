using Conciliador.Domain.Common;

namespace Conciliador.Domain.Reconciliations;

public sealed class Reconciliation
{
    public const int MaxRangeInDays = 366;

    private readonly List<ReconciliationLine> _lines = new();
    private readonly List<ReconciliationIssue> _issues = new();
    private readonly List<SourceDocument> _sources = new();

    private Reconciliation() { }

    public Guid Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public OperationType Operation { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal TotalSales { get; private set; }
    public decimal TotalStatement { get; private set; }
    public decimal TotalDifference { get; private set; }
    public int MatchedLines { get; private set; }
    public int DivergentLines { get; private set; }

    public IReadOnlyCollection<ReconciliationLine> Lines => _lines;
    public IReadOnlyCollection<ReconciliationIssue> Issues => _issues;
    public IReadOnlyCollection<SourceDocument> Sources => _sources;

    public bool IsFullyMatched => _lines.Count > 0 && DivergentLines == 0;

    public static Reconciliation Create(
        OperationType operation,
        DateOnly startDate,
        DateOnly endDate,
        string createdBy,
        DateTime createdAtUtc,
        IEnumerable<ReconciliationLine> lines,
        IEnumerable<ReconciliationIssue> issues,
        IEnumerable<SourceDocument> sources)
    {
        EnsureValidRange(startDate, endDate);

        var reconciliation = new Reconciliation
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc,
            CreatedBy = createdBy,
            Operation = operation,
            StartDate = startDate,
            EndDate = endDate
        };

        reconciliation._lines.AddRange(lines);
        reconciliation._issues.AddRange(issues);
        reconciliation._sources.AddRange(sources);

        reconciliation.TotalSales = reconciliation._lines.Sum(l => l.SalesAmount);
        reconciliation.TotalStatement = reconciliation._lines.Sum(l => l.StatementAmount);
        reconciliation.TotalDifference = reconciliation.TotalSales - reconciliation.TotalStatement;
        reconciliation.MatchedLines = reconciliation._lines.Count(l => l.Status == LineStatus.Matched);
        reconciliation.DivergentLines = reconciliation._lines.Count - reconciliation.MatchedLines;

        return reconciliation;
    }

    public static void EnsureValidRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new DomainException("A data final não pode ser anterior à data inicial.");

        if (endDate.DayNumber - startDate.DayNumber + 1 > MaxRangeInDays)
            throw new DomainException($"O intervalo não pode passar de {MaxRangeInDays} dias.");
    }
}
