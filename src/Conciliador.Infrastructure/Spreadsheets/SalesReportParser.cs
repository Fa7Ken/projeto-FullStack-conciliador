using ClosedXML.Excel;
using Conciliador.Application.Abstractions;
using Conciliador.Application.Common;
using Conciliador.Domain.Reconciliations;

namespace Conciliador.Infrastructure.Spreadsheets;

// Le o "Relatorio Detalhado de Vendas" da maquininha.
// As colunas sao localizadas pelo titulo, nao pela letra, para tolerar mudancas de ordem.
public sealed class SalesReportParser : ISalesReportParser
{
    private const int HeaderSearchLimit = 20;

    private const string SaleDateHeader = "data da venda";
    private const string CardTypeHeader = "tipo de cartao";
    private const string BrandHeader = "bandeira";
    private const string NetAmountHeader = "valor liquido da parcela/transacao";
    private const string PaymentDateHeader = "data efetiva do pagamento da venda";

    private static readonly string[] RequiredHeaders =
        { SaleDateHeader, CardTypeHeader, BrandHeader, NetAmountHeader, PaymentDateHeader };

    public SalesReport Parse(Stream content, string fileName)
    {
        using var workbook = WorkbookOpener.Open(content, fileName);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new SpreadsheetFormatException(fileName, $"\"{fileName}\" não tem nenhuma aba.");

        var (headerRow, columns) = FindHeader(sheet, fileName);
        var (periodStart, periodEnd) = PeriodReader.Read(sheet, headerRow);

        var entries = new List<SaleEntry>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);

            // Linha sem data de venda e rodape ou linha em branco
            if (!CellReader.TryDate(row.Cell(columns[SaleDateHeader]), out var saleDate)) continue;

            var operation = ParseOperation(CellReader.Text(row.Cell(columns[CardTypeHeader])));
            if (operation is null) continue;

            var brandText = CellReader.Text(row.Cell(columns[BrandHeader]));
            if (brandText.Length == 0) continue;

            if (!CellReader.TryDecimal(row.Cell(columns[NetAmountHeader]), out var netAmount))
            {
                throw new SpreadsheetFormatException(fileName,
                    $"\"{fileName}\", linha {rowNumber}: o valor líquido não é um número.");
            }

            // Venda ainda nao paga vem com "-" na data efetiva e fica fora da conciliacao
            DateOnly? paymentDate = CellReader.TryDate(row.Cell(columns[PaymentDateHeader]), out var paid) ? paid : null;

            entries.Add(new SaleEntry(saleDate, operation.Value, CardBrand.Normalize(brandText), netAmount, paymentDate));
        }

        var source = new SourceDocument(fileName, SourceKind.SalesReport, periodStart, periodEnd, entries.Count);
        return new SalesReport(source, entries);
    }

    private static (int HeaderRow, Dictionary<string, int> Columns) FindHeader(IXLWorksheet sheet, string fileName)
    {
        for (var rowNumber = 1; rowNumber <= HeaderSearchLimit; rowNumber++)
        {
            var columns = new Dictionary<string, int>();
            foreach (var cell in sheet.Row(rowNumber).CellsUsed())
            {
                // "Tipo de cartao" aparece duas vezes no arquivo; vale a primeira (debito/credito)
                columns.TryAdd(CellReader.Normalize(CellReader.Text(cell)), cell.Address.ColumnNumber);
            }

            if (!columns.ContainsKey(SaleDateHeader)) continue;

            var missing = RequiredHeaders.Where(h => !columns.ContainsKey(h)).ToList();
            if (missing.Count > 0)
            {
                throw new SpreadsheetFormatException(fileName,
                    $"\"{fileName}\" não tem a(s) coluna(s) esperada(s) do relatório de vendas: {string.Join(", ", missing)}.");
            }

            return (rowNumber, columns);
        }

        throw new SpreadsheetFormatException(fileName,
            $"\"{fileName}\" não parece ser um relatório de vendas: a coluna \"Data da venda\" não foi encontrada.");
    }

    private static OperationType? ParseOperation(string text)
    {
        var normalized = CellReader.Normalize(text);
        if (normalized.StartsWith("deb", StringComparison.Ordinal)) return OperationType.Debit;
        if (normalized.StartsWith("cred", StringComparison.Ordinal)) return OperationType.Credit;
        return null;
    }
}
