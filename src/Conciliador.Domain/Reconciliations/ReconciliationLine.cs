namespace Conciliador.Domain.Reconciliations;

// Resultado de um dia de pagamento para uma bandeira
public sealed class ReconciliationLine
{
    private ReconciliationLine() { }

    public DateOnly Date { get; private set; }
    public string Brand { get; private set; } = string.Empty;
    public decimal SalesAmount { get; private set; }
    public decimal StatementAmount { get; private set; }
    public int SalesCount { get; private set; }
    public decimal Difference { get; private set; }
    public LineStatus Status { get; private set; }

    // salesAmount ou statementAmount nulo significa que aquele lado nao tem lancamento no dia
    public static ReconciliationLine Create(DateOnly date, string brand, decimal? salesAmount, int salesCount, decimal? statementAmount)
    {
        var sales = Math.Round(salesAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
        var statement = Math.Round(statementAmount ?? 0m, 2, MidpointRounding.AwayFromZero);
        var difference = sales - statement;

        LineStatus status;
        if (salesAmount is null) status = LineStatus.MissingSales;
        else if (statementAmount is null) status = LineStatus.MissingStatement;
        else if (difference == 0m) status = LineStatus.Matched;
        else if (difference > 0m) status = LineStatus.SalesAboveStatement;
        else status = LineStatus.StatementAboveSales;

        return new ReconciliationLine
        {
            Date = date,
            Brand = brand,
            SalesAmount = sales,
            StatementAmount = statement,
            SalesCount = salesCount,
            Difference = difference,
            Status = status
        };
    }
}
