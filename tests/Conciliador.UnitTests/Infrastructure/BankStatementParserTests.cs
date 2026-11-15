using Conciliador.Application.Common;
using Conciliador.Domain.Reconciliations;
using Conciliador.Infrastructure.Spreadsheets;
using Conciliador.TestData;
using FluentAssertions;

namespace Conciliador.UnitTests.Infrastructure;

public class BankStatementParserTests
{
    private static readonly DateOnly Start = new(2026, 3, 1);
    private static readonly DateOnly End = new(2026, 3, 31);
    private static readonly DateOnly Day = new(2026, 3, 10);

    private readonly BankStatementParser _parser = new();

    [Fact]
    public void Keeps_only_card_settlements_and_discards_the_noise()
    {
        var workbook = StatementWorkbook.Build(Start, End, new[]
        {
            new SampleStatementRow(Day, "RECEBIMENTO PIX 00000000001 CLIENTE EXEMPLO", "PIX_CRED", 16.32m),
            new SampleStatementRow(Day, StatementWorkbook.CardDescription("CREDITO", "ELO"), "1", 563.77m),
            new SampleStatementRow(Day, StatementWorkbook.CardDescription("DEBITO", "MASTER"), "2", 24721.85m),
            new SampleStatementRow(Day, "LIQUIDACAO BOLETO 00000000000100 FORNECEDOR", " ", -900m),
            new SampleStatementRow(Day, "DEBITO CONVENIOS OPERADORA", "CONV", -120m),
            new SampleStatementRow(Day, "TED 000.000.000-00 CREDITO VISA LTDA", "3", 50m)
        });

        var statement = Parse(workbook);

        statement.Source.PeriodStart.Should().Be(Start);
        statement.Source.PeriodEnd.Should().Be(End);
        statement.Entries.Should().HaveCount(2);
        statement.Entries.Should().ContainSingle(e => e.Operation == OperationType.Credit && e.Brand == "Elo" && e.Amount == 563.77m);
        statement.Entries.Should().ContainSingle(e => e.Operation == OperationType.Debit && e.Brand == "Mastercard" && e.Amount == 24721.85m);
    }

    [Fact]
    public void Stops_reading_at_the_future_entries_block()
    {
        // O gerador coloca uma linha com cara de repasse dentro do bloco de lancamentos futuros
        var workbook = StatementWorkbook.Build(Start, End, new[]
        {
            new SampleStatementRow(Day, StatementWorkbook.CardDescription("DEBITO", "VISA"), "1", 100m)
        }, includeFutureEntries: true);

        Parse(workbook).Entries.Should().ContainSingle().Which.Amount.Should().Be(100m);
    }

    [Fact]
    public void Rejects_a_sales_report_sent_as_statement()
    {
        var salesReport = SalesReportWorkbook.Build(Start, End, Array.Empty<SampleSale>());

        var act = () => Parse(salesReport);

        act.Should().Throw<SpreadsheetFormatException>().WithMessage("*não parece ser um extrato*");
    }

    private BankStatement Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return _parser.Parse(stream, "extrato.xlsx");
    }
}
