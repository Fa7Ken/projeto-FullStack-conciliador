using Conciliador.Domain.Common;
using Conciliador.Domain.Reconciliations;
using FluentAssertions;

namespace Conciliador.UnitTests.Domain;

public class ReconciliationEngineTests
{
    private static readonly DateOnly Start = new(2026, 3, 1);
    private static readonly DateOnly End = new(2026, 3, 31);
    private static readonly DateOnly Day = new(2026, 3, 10);

    private readonly ReconciliationEngine _engine = new();

    [Fact]
    public void Sums_sales_by_payment_date_and_matches_statement()
    {
        var sales = Sales(
            Sale(OperationType.Debit, "Visa", 10.50m, Day),
            Sale(OperationType.Debit, "Visa", 20.25m, Day),
            Sale(OperationType.Debit, "Mastercard", 5.00m, Day));
        var statement = Statement(
            Settlement(OperationType.Debit, "Visa", 30.75m, Day),
            Settlement(OperationType.Debit, "Mastercard", 5.00m, Day));

        var result = Run(OperationType.Debit, sales, statement);

        result.Lines.Should().HaveCount(2).And.OnlyContain(l => l.Status == LineStatus.Matched && l.Difference == 0m);
        result.Lines.Single(l => l.Brand == "Visa").SalesCount.Should().Be(2);
        result.IsFullyMatched.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Difference_is_sales_minus_statement()
    {
        var result = Run(OperationType.Debit,
            Sales(Sale(OperationType.Debit, "Visa", 100m, Day), Sale(OperationType.Debit, "Elo", 50m, Day)),
            Statement(Settlement(OperationType.Debit, "Visa", 90m, Day), Settlement(OperationType.Debit, "Elo", 62.5m, Day)));

        var visa = result.Lines.Single(l => l.Brand == "Visa");
        visa.Difference.Should().Be(10m);
        visa.Status.Should().Be(LineStatus.SalesAboveStatement);

        var elo = result.Lines.Single(l => l.Brand == "Elo");
        elo.Difference.Should().Be(-12.5m);
        elo.Status.Should().Be(LineStatus.StatementAboveSales);

        result.TotalDifference.Should().Be(-2.5m);
        result.DivergentLines.Should().Be(2);
        result.IsFullyMatched.Should().BeFalse();
    }

    [Fact]
    public void Flags_days_present_on_only_one_side()
    {
        var other = Day.AddDays(1);
        var result = Run(OperationType.Debit,
            Sales(Sale(OperationType.Debit, "Visa", 40m, Day)),
            Statement(Settlement(OperationType.Debit, "Visa", 70m, other)));

        result.Lines.Single(l => l.Date == Day).Status.Should().Be(LineStatus.MissingStatement);
        result.Lines.Single(l => l.Date == other).Status.Should().Be(LineStatus.MissingSales);
        result.Issues.Select(i => i.Code).Should().Contain(new[] { IssueCodes.DaysMissingSales, IssueCodes.DaysMissingStatement });
    }

    [Fact]
    public void Ignores_other_operation_other_dates_and_unpaid_sales()
    {
        var sales = Sales(
            Sale(OperationType.Debit, "Visa", 10m, Day),
            Sale(OperationType.Credit, "Visa", 999m, Day),
            Sale(OperationType.Debit, "Visa", 999m, End.AddDays(1)),
            Sale(OperationType.Debit, "Visa", 999m, null));
        var statement = Statement(
            Settlement(OperationType.Debit, "Visa", 10m, Day),
            Settlement(OperationType.Credit, "Visa", 555m, Day),
            Settlement(OperationType.Debit, "Visa", 555m, Start.AddDays(-1)));

        var result = Run(OperationType.Debit, sales, statement);

        result.Lines.Should().ContainSingle().Which.Status.Should().Be(LineStatus.Matched);
        result.TotalSales.Should().Be(10m);
    }

    [Fact]
    public void Merges_several_files_per_side()
    {
        var february = new SalesReport(Source(SourceKind.SalesReport, new(2026, 2, 1), new(2026, 2, 28)),
            new[] { Sale(OperationType.Credit, "Visa", 60m, Day) });
        var march = new SalesReport(Source(SourceKind.SalesReport, Start, End),
            new[] { Sale(OperationType.Credit, "Visa", 40m, Day) });

        var result = _engine.Reconcile(OperationType.Credit, Start, End, new[] { february, march },
            new[] { Statement(Settlement(OperationType.Credit, "Visa", 100m, Day)) }, "user@test", DateTime.UtcNow);

        result.Lines.Should().ContainSingle().Which.Status.Should().Be(LineStatus.Matched);
        result.Sources.Should().HaveCount(3);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Reports_error_when_no_side_has_data_in_range()
    {
        var result = Run(OperationType.Debit, Sales(), Statement());

        result.Lines.Should().BeEmpty();
        result.IsFullyMatched.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == IssueCodes.NoDataInRange && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Reports_error_when_only_statement_has_data()
    {
        var result = Run(OperationType.Debit, Sales(), Statement(Settlement(OperationType.Debit, "Visa", 10m, Day)));

        result.Issues.Should().Contain(i => i.Code == IssueCodes.NoSalesInRange);
        result.Issues.Should().NotContain(i => i.Code == IssueCodes.DaysMissingSales);
    }

    [Fact]
    public void Reports_error_when_only_sales_have_data()
    {
        var result = Run(OperationType.Debit, Sales(Sale(OperationType.Debit, "Visa", 10m, Day)), Statement());

        result.Issues.Should().Contain(i => i.Code == IssueCodes.NoStatementInRange);
    }

    [Theory]
    [InlineData(OperationType.Credit, 2026, 2, 2, true)]   // credito precisa de 28 dias antes
    [InlineData(OperationType.Credit, 2026, 2, 1, false)]
    [InlineData(OperationType.Debit, 2026, 2, 26, true)]   // debito precisa de poucos dias antes
    [InlineData(OperationType.Debit, 2026, 2, 25, false)]
    public void Warns_when_sales_reports_start_too_late_for_the_range(
        OperationType operation, int year, int month, int day, bool expectWarning)
    {
        var report = new SalesReport(Source(SourceKind.SalesReport, new DateOnly(year, month, day), End),
            new[] { Sale(operation, "Visa", 10m, Day) });

        var result = _engine.Reconcile(operation, Start, End, new[] { report },
            new[] { Statement(Settlement(operation, "Visa", 10m, Day)) }, "user@test", DateTime.UtcNow);

        result.Issues.Any(i => i.Code == IssueCodes.SalesCoverageGap).Should().Be(expectWarning);
    }

    [Fact]
    public void Warns_when_statement_does_not_cover_the_whole_range()
    {
        var statement = new BankStatement(Source(SourceKind.BankStatement, Start, new(2026, 3, 15)),
            new[] { Settlement(OperationType.Debit, "Visa", 10m, Day) });

        var result = _engine.Reconcile(OperationType.Debit, Start, End,
            new[] { Sales(Sale(OperationType.Debit, "Visa", 10m, Day)) }, new[] { statement }, "user@test", DateTime.UtcNow);

        result.Issues.Should().ContainSingle(i => i.Code == IssueCodes.RangeOutsideStatementPeriod)
            .Which.Message.Should().Contain("16 dia(s)").And.Contain("16/03/2026").And.Contain("31/03/2026");
    }

    [Theory]
    [InlineData(OperationType.Credit, true)]
    [InlineData(OperationType.Debit, false)]
    public void Hints_at_installment_remnants_only_for_credit(OperationType operation, bool expectHint)
    {
        var result = Run(operation,
            Sales(Sale(operation, "Visa", 100m, Day)),
            Statement(Settlement(operation, "Visa", 130m, Day)));

        result.Issues.Any(i => i.Code == IssueCodes.PossibleInstallmentRemnants).Should().Be(expectHint);
    }

    [Fact]
    public void Orders_lines_by_date_then_brand()
    {
        var later = Day.AddDays(2);
        var result = Run(OperationType.Debit,
            Sales(Sale(OperationType.Debit, "Visa", 1m, later), Sale(OperationType.Debit, "Visa", 1m, Day),
                Sale(OperationType.Debit, "Elo", 1m, Day)),
            Statement());

        result.Lines.Select(l => (l.Date, l.Brand)).Should().Equal((Day, "Elo"), (Day, "Visa"), (later, "Visa"));
    }

    [Fact]
    public void Rejects_inverted_or_oversized_ranges()
    {
        var inverted = () => _engine.Reconcile(OperationType.Debit, End, Start, Array.Empty<SalesReport>(),
            Array.Empty<BankStatement>(), "user@test", DateTime.UtcNow);
        var oversized = () => _engine.Reconcile(OperationType.Debit, Start, Start.AddDays(Reconciliation.MaxRangeInDays),
            Array.Empty<SalesReport>(), Array.Empty<BankStatement>(), "user@test", DateTime.UtcNow);

        inverted.Should().Throw<DomainException>();
        oversized.Should().Throw<DomainException>();
    }

    private Reconciliation Run(OperationType operation, SalesReport sales, BankStatement statement) =>
        _engine.Reconcile(operation, Start, End, new[] { sales }, new[] { statement }, "user@test", DateTime.UtcNow);

    // Periodo comecando em janeiro para os testes comuns nao dispararem o aviso de cobertura
    private static SalesReport Sales(params SaleEntry[] entries) =>
        new(Source(SourceKind.SalesReport, new(2026, 1, 1), End), entries);

    private static BankStatement Statement(params StatementEntry[] entries) =>
        new(Source(SourceKind.BankStatement, Start, End), entries);

    private static SourceDocument Source(SourceKind kind, DateOnly start, DateOnly end) =>
        new($"{kind}.xlsx", kind, start, end, 0);

    private static SaleEntry Sale(OperationType operation, string brand, decimal net, DateOnly? paidOn) =>
        new(new DateOnly(2026, 2, 20), operation, brand, net, paidOn);

    private static StatementEntry Settlement(OperationType operation, string brand, decimal amount, DateOnly date) =>
        new(date, operation, brand, amount, "card settlement");
}
