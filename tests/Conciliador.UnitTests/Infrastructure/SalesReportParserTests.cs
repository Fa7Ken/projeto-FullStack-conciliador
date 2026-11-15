using ClosedXML.Excel;
using Conciliador.Application.Common;
using Conciliador.Domain.Reconciliations;
using Conciliador.Infrastructure.Spreadsheets;
using Conciliador.TestData;
using FluentAssertions;

namespace Conciliador.UnitTests.Infrastructure;

public class SalesReportParserTests
{
    private static readonly DateOnly Start = new(2026, 3, 1);
    private static readonly DateOnly End = new(2026, 3, 31);

    private readonly SalesReportParser _parser = new();

    [Fact]
    public void Reads_operation_brand_net_amount_and_effective_payment_date()
    {
        var workbook = SalesReportWorkbook.Build(Start, End, new[]
        {
            new SampleSale(new(2026, 3, 2), new(8, 0), "Débito", "-", "Débito", "Mastercard", 100m, 0.0077m, new(2026, 3, 3), new(2026, 3, 4)),
            new SampleSale(new(2026, 3, 2), new(8, 5), "Crédito a Vista", "-", "Crédito", "ELO", 50m, 0.0345m, new(2026, 4, 1), null)
        });

        var report = Parse(workbook);

        report.Source.PeriodStart.Should().Be(Start);
        report.Source.PeriodEnd.Should().Be(End);
        report.Source.EntryCount.Should().Be(2);

        // Usa a data efetiva (04/03), nao a prevista (03/03), e o valor liquido (100 - 0,77)
        report.Entries[0].Should().Be(new SaleEntry(new(2026, 3, 2), OperationType.Debit, "Mastercard", 99.23m, new(2026, 3, 4)));
        report.Entries[1].Operation.Should().Be(OperationType.Credit);
        report.Entries[1].Brand.Should().Be("Elo");
        report.Entries[1].PaymentDate.Should().BeNull();
    }

    [Fact]
    public void Finds_columns_by_title_even_when_the_order_changes()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Vendas");
        sheet.Cell(1, 1).Value = "Período selecionado: 01/03/2026 à 31/03/2026";
        var headers = new[] { "Bandeira", "Data efetiva do pagamento da venda", "Tipo de cartão",
            "Valor líquido da parcela/transação", "Data da venda", "Tipo de cartão" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(3, i + 1).Value = headers[i];
        sheet.Cell(4, 1).Value = "Visa";
        sheet.Cell(4, 2).Value = "05/03/2026";
        sheet.Cell(4, 3).Value = "Crédito";
        sheet.Cell(4, 4).Value = "12,34";
        sheet.Cell(4, 5).Value = "03/02/2026";
        sheet.Cell(4, 6).Value = "Nacional";

        var report = Parse(Save(workbook));

        report.Entries.Should().ContainSingle()
            .Which.Should().Be(new SaleEntry(new(2026, 2, 3), OperationType.Credit, "Visa", 12.34m, new(2026, 3, 5)));
    }

    [Fact]
    public void Explains_which_columns_are_missing()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Vendas");
        sheet.Cell(1, 1).Value = "Data da venda";
        sheet.Cell(1, 2).Value = "Bandeira";

        var act = () => Parse(Save(workbook));

        act.Should().Throw<SpreadsheetFormatException>()
            .WithMessage("*tipo de cartao*").WithMessage("*data efetiva do pagamento da venda*");
    }

    [Fact]
    public void Rejects_a_statement_sent_as_sales_report()
    {
        var statement = StatementWorkbook.Build(Start, End, Array.Empty<SampleStatementRow>());

        var act = () => Parse(statement);

        act.Should().Throw<SpreadsheetFormatException>().WithMessage("*não parece ser um relatório de vendas*");
    }

    [Fact]
    public void Rejects_a_file_that_is_not_a_spreadsheet()
    {
        var act = () => Parse("not a spreadsheet"u8.ToArray());

        act.Should().Throw<SpreadsheetFormatException>().WithMessage("*Não foi possível abrir*");
    }

    private SalesReport Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return _parser.Parse(stream, "vendas.xlsx");
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
