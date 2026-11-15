using Conciliador.Domain.Reconciliations;
using Conciliador.Infrastructure.Spreadsheets;
using Conciliador.TestData;
using FluentAssertions;

namespace Conciliador.UnitTests.Infrastructure;

// Fluxo completo com as planilhas ficticias: gerar, ler e conciliar
public class SampleScenarioTests
{
    private static readonly SampleScenario Scenario = SampleScenario.Create(salesPerDay: 12);
    private static readonly SalesReport February = ParseSales(Scenario.FebruarySalesWorkbook(), "fev.xlsx");
    private static readonly SalesReport March = ParseSales(Scenario.MarchSalesWorkbook(), "mar.xlsx");
    private static readonly BankStatement Statement = ParseStatement(Scenario.MarchStatementWorkbook());

    private readonly ReconciliationEngine _engine = new();

    [Fact]
    public void Debit_matches_except_for_the_pos_rental_discount()
    {
        var result = Reconcile(OperationType.Debit, February, March);

        var divergent = result.Lines.Where(l => l.Status != LineStatus.Matched).ToList();
        divergent.Should().ContainSingle();
        divergent[0].Date.Should().Be(Scenario.PosRentalDay);
        divergent[0].Brand.Should().Be("Mastercard");
        divergent[0].Difference.Should().Be(SampleScenario.PosRentalDiscount);
        result.MatchedLines.Should().BeGreaterThan(50);
    }

    [Fact]
    public void Credit_shows_the_missing_settlement_and_the_installment_remnant()
    {
        var result = Reconcile(OperationType.Credit, February, March);

        var divergent = result.Lines.Where(l => l.Status != LineStatus.Matched).ToList();
        divergent.Should().HaveCount(2);
        divergent.Should().ContainSingle(l => l.Date == Scenario.MissingCreditVisaDay && l.Brand == "Visa" && l.Status == LineStatus.MissingStatement);
        divergent.Should().ContainSingle(l => l.Date == Scenario.RemnantDay && l.Brand == "Elo" && l.Difference == -SampleScenario.InstallmentRemnant);
        result.Issues.Select(i => i.Code).Should().Contain(IssueCodes.PossibleInstallmentRemnants);
    }

    [Fact]
    public void Credit_without_the_previous_month_report_warns_about_coverage()
    {
        var result = Reconcile(OperationType.Credit, March);

        result.Issues.Select(i => i.Code).Should().Contain(IssueCodes.SalesCoverageGap);
        result.DivergentLines.Should().BeGreaterThan(10);
    }

    private Reconciliation Reconcile(OperationType operation, params SalesReport[] reports) =>
        _engine.Reconcile(operation, SampleScenario.MarchStart, SampleScenario.MarchEnd, reports, new[] { Statement },
            "user@test", DateTime.UtcNow);

    private static SalesReport ParseSales(byte[] content, string name)
    {
        using var stream = new MemoryStream(content);
        return new SalesReportParser().Parse(stream, name);
    }

    private static BankStatement ParseStatement(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return new BankStatementParser().Parse(stream, "extrato.xlsx");
    }
}
