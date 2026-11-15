namespace Conciliador.Domain.Reconciliations;

// Aviso exibido ao usuario explicando por que algo pode nao bater
public sealed class ReconciliationIssue
{
    private ReconciliationIssue() { }

    public ReconciliationIssue(IssueSeverity severity, string code, string message)
    {
        Severity = severity;
        Code = code;
        Message = message;
    }

    public IssueSeverity Severity { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
}

public static class IssueCodes
{
    public const string NoDataInRange = "NO_DATA_IN_RANGE";
    public const string NoSalesInRange = "NO_SALES_IN_RANGE";
    public const string NoStatementInRange = "NO_STATEMENT_IN_RANGE";
    public const string RangeOutsideStatementPeriod = "RANGE_OUTSIDE_STATEMENT_PERIOD";
    public const string SalesCoverageGap = "SALES_COVERAGE_GAP";
    public const string DaysMissingSales = "DAYS_MISSING_SALES";
    public const string DaysMissingStatement = "DAYS_MISSING_STATEMENT";
    public const string PossibleInstallmentRemnants = "POSSIBLE_INSTALLMENT_REMNANTS";
}
