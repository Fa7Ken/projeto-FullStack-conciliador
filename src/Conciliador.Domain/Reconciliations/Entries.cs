namespace Conciliador.Domain.Reconciliations;

// Uma linha do relatorio de vendas (uma venda ou uma parcela)
public sealed record SaleEntry(
    DateOnly SaleDate,
    OperationType Operation,
    string Brand,
    decimal NetAmount,
    DateOnly? PaymentDate);

// Um repasse de cartao encontrado no extrato bancario
public sealed record StatementEntry(
    DateOnly Date,
    OperationType Operation,
    string Brand,
    decimal Amount,
    string Description);

public sealed record SalesReport(SourceDocument Source, IReadOnlyList<SaleEntry> Entries);

public sealed record BankStatement(SourceDocument Source, IReadOnlyList<StatementEntry> Entries);
