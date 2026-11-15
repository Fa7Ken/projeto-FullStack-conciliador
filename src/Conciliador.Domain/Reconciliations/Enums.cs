namespace Conciliador.Domain.Reconciliations;

public enum OperationType
{
    Debit = 1,
    Credit = 2
}

public enum LineStatus
{
    Matched = 1,
    SalesAboveStatement = 2,
    StatementAboveSales = 3,
    MissingStatement = 4,
    MissingSales = 5
}

public enum IssueSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3
}

public enum SourceKind
{
    SalesReport = 1,
    BankStatement = 2
}
