using System.Globalization;

namespace Conciliador.Domain.Reconciliations;

// Regra central: soma as vendas pelo dia efetivo de pagamento e compara, dia a dia e por bandeira,
// com os repasses do extrato. Diferenca = vendas (liquido) - extrato.
public sealed class ReconciliationEngine
{
    // Debito cai no proximo dia util; a folga cobre fim de semana e feriado
    private const int DebitLookbackDays = 4;

    // Credito a vista cai cerca de 30 dias depois da venda. Usa 28 para avisar quando falta o mes
    // anterior inteiro, sem acusar falta por causa de um ou dois dias na virada do mes
    private const int CreditLookbackDays = 28;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public Reconciliation Reconcile(
        OperationType operation,
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyCollection<SalesReport> salesReports,
        IReadOnlyCollection<BankStatement> statements,
        string createdBy,
        DateTime nowUtc)
    {
        Reconciliation.EnsureValidRange(startDate, endDate);

        var salesByKey = salesReports
            .SelectMany(r => r.Entries)
            .Where(e => e.Operation == operation && e.PaymentDate is { } paid && paid >= startDate && paid <= endDate)
            .GroupBy(e => (Date: e.PaymentDate!.Value, e.Brand))
            .ToDictionary(g => g.Key, g => (Amount: g.Sum(e => e.NetAmount), Count: g.Count()));

        var statementByKey = statements
            .SelectMany(s => s.Entries)
            .Where(e => e.Operation == operation && e.Date >= startDate && e.Date <= endDate)
            .GroupBy(e => (e.Date, e.Brand))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var lines = salesByKey.Keys
            .Union(statementByKey.Keys)
            .OrderBy(k => k.Date)
            .ThenBy(k => k.Brand, StringComparer.Ordinal)
            .Select(key =>
            {
                var hasSales = salesByKey.TryGetValue(key, out var sales);
                var hasStatement = statementByKey.TryGetValue(key, out var statementAmount);
                return ReconciliationLine.Create(
                    key.Date,
                    key.Brand,
                    hasSales ? sales.Amount : null,
                    hasSales ? sales.Count : 0,
                    hasStatement ? statementAmount : null);
            })
            .ToList();

        var issues = BuildIssues(operation, startDate, endDate, salesReports, statements, lines,
            salesByKey.Count > 0, statementByKey.Count > 0);

        var sources = salesReports.Select(r => r.Source).Concat(statements.Select(s => s.Source));

        return Reconciliation.Create(operation, startDate, endDate, createdBy, nowUtc, lines, issues, sources);
    }

    private static List<ReconciliationIssue> BuildIssues(
        OperationType operation,
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyCollection<SalesReport> salesReports,
        IReadOnlyCollection<BankStatement> statements,
        IReadOnlyCollection<ReconciliationLine> lines,
        bool hasSales,
        bool hasStatement)
    {
        var issues = new List<ReconciliationIssue>();
        var operationName = operation == OperationType.Debit ? "débito" : "crédito";
        var range = $"{Format(startDate)} a {Format(endDate)}";

        if (!hasSales && !hasStatement)
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Error, IssueCodes.NoDataInRange,
                $"Nenhum lançamento de {operationName} foi encontrado no período de {range}, nem nas vendas nem no extrato. " +
                "As planilhas enviadas não coincidem com o intervalo selecionado."));
        }
        else if (!hasSales)
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Error, IssueCodes.NoSalesInRange,
                $"O extrato tem repasses de {operationName} no período de {range}, mas nenhum relatório de vendas enviado tem " +
                "pagamentos efetivos nesse intervalo. Verifique se os relatórios correspondem ao período."));
        }
        else if (!hasStatement)
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Error, IssueCodes.NoStatementInRange,
                $"Os relatórios de vendas têm pagamentos de {operationName} no período de {range}, mas o extrato enviado não tem " +
                "repasses nesse intervalo. Verifique se o extrato corresponde ao período."));
        }

        AddStatementCoverageIssue(issues, startDate, endDate, statements);
        AddSalesCoverageIssue(issues, operation, operationName, startDate, salesReports);

        var missingSales = lines.Count(l => l.Status == LineStatus.MissingSales);
        if (missingSales > 0 && hasSales)
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Warning, IssueCodes.DaysMissingSales,
                $"{missingSales} repasse(s) do extrato não têm nenhuma venda correspondente no mesmo dia e bandeira."));
        }

        var missingStatement = lines.Count(l => l.Status == LineStatus.MissingStatement);
        if (missingStatement > 0 && hasStatement)
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Warning, IssueCodes.DaysMissingStatement,
                $"{missingStatement} dia(s) com vendas pagas não têm repasse correspondente no extrato."));
        }

        if (operation == OperationType.Credit && lines.Any(l => l.Status == LineStatus.StatementAboveSales))
        {
            issues.Add(new ReconciliationIssue(IssueSeverity.Info, IssueCodes.PossibleInstallmentRemnants,
                "Há dias em que o extrato é maior que as vendas. No crédito isso costuma ser parcela de venda " +
                "de um mês cujo relatório não foi enviado."));
        }

        return issues;
    }

    private static void AddStatementCoverageIssue(
        List<ReconciliationIssue> issues, DateOnly startDate, DateOnly endDate, IReadOnlyCollection<BankStatement> statements)
    {
        var periods = statements
            .Where(s => s.Source.PeriodStart is not null && s.Source.PeriodEnd is not null)
            .Select(s => (Start: s.Source.PeriodStart!.Value, End: s.Source.PeriodEnd!.Value))
            .ToList();

        // Sem periodo declarado no arquivo nao ha como avaliar a cobertura
        if (periods.Count == 0) return;

        var uncovered = new List<DateOnly>();
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            if (!periods.Any(p => day >= p.Start && day <= p.End))
                uncovered.Add(day);
        }

        if (uncovered.Count == 0) return;

        issues.Add(new ReconciliationIssue(IssueSeverity.Warning, IssueCodes.RangeOutsideStatementPeriod,
            $"O extrato enviado não cobre {uncovered.Count} dia(s) do intervalo " +
            $"(de {Format(uncovered[0])} a {Format(uncovered[^1])}). Envie também o extrato desse período."));
    }

    private static void AddSalesCoverageIssue(
        List<ReconciliationIssue> issues, OperationType operation, string operationName, DateOnly startDate,
        IReadOnlyCollection<SalesReport> salesReports)
    {
        var starts = salesReports
            .Where(r => r.Source.PeriodStart is not null)
            .Select(r => r.Source.PeriodStart!.Value)
            .ToList();

        if (starts.Count == 0) return;

        var lookback = operation == OperationType.Credit ? CreditLookbackDays : DebitLookbackDays;
        var neededFrom = startDate.AddDays(-lookback);
        var earliest = starts.Min();

        if (earliest <= neededFrom) return;

        issues.Add(new ReconciliationIssue(IssueSeverity.Warning, IssueCodes.SalesCoverageGap,
            $"Os relatórios de vendas enviados começam em {Format(earliest)}. Repasses de {operationName} do início do " +
            $"intervalo vêm de vendas feitas a partir de {Format(neededFrom)}, então o relatório do período anterior " +
            "parece estar faltando e os primeiros dias podem não bater."));
    }

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", PtBr);
}
