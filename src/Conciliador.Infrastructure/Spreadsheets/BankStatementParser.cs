using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Conciliador.Application.Abstractions;
using Conciliador.Application.Common;
using Conciliador.Domain.Reconciliations;

namespace Conciliador.Infrastructure.Spreadsheets;

// Le o extrato bancario e guarda apenas os repasses de cartao.
// PIX, boletos, TEDs e o bloco de lancamentos futuros sao descartados.
public sealed partial class BankStatementParser : IBankStatementParser
{
    private const int HeaderSearchLimit = 30;

    // Ex.: "SICREDI CREDITO ELO | SICREDI | 0001-70" -> operacao e bandeira
    [GeneratedRegex(@"^\s*\w+\s+(credito|debito)\s+([a-z ]+?)\s*\|")]
    private static partial Regex CardSettlementRegex();

    public BankStatement Parse(Stream content, string fileName)
    {
        using var workbook = WorkbookOpener.Open(content, fileName);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new SpreadsheetFormatException(fileName, $"\"{fileName}\" não tem nenhuma aba.");

        var (headerRow, dateColumn, descriptionColumn, amountColumn) = FindHeader(sheet, fileName);
        var (periodStart, periodEnd) = PeriodReader.Read(sheet, headerRow);

        var entries = new List<StatementEntry>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var firstCell = CellReader.Normalize(CellReader.Text(row.Cell(dateColumn)));

            // Daqui para baixo sao previsoes, nao movimentacao efetiva
            if (firstCell.StartsWith("lancamentos futuros", StringComparison.Ordinal)) break;

            var description = CellReader.Text(row.Cell(descriptionColumn));
            var match = CardSettlementRegex().Match(CellReader.Normalize(description));
            if (!match.Success) continue;

            if (!CellReader.TryDate(row.Cell(dateColumn), out var date))
            {
                throw new SpreadsheetFormatException(fileName,
                    $"\"{fileName}\", linha {rowNumber}: a data do lançamento não foi reconhecida.");
            }

            if (!CellReader.TryDecimal(row.Cell(amountColumn), out var amount))
            {
                throw new SpreadsheetFormatException(fileName,
                    $"\"{fileName}\", linha {rowNumber}: o valor do lançamento não é um número.");
            }

            var operation = match.Groups[1].Value == "debito" ? OperationType.Debit : OperationType.Credit;
            var brand = CardBrand.Normalize(match.Groups[2].Value);

            entries.Add(new StatementEntry(date, operation, brand, amount, description));
        }

        var source = new SourceDocument(fileName, SourceKind.BankStatement, periodStart, periodEnd, entries.Count);
        return new BankStatement(source, entries);
    }

    private static (int HeaderRow, int Date, int Description, int Amount) FindHeader(IXLWorksheet sheet, string fileName)
    {
        for (var rowNumber = 1; rowNumber <= HeaderSearchLimit; rowNumber++)
        {
            int? date = null, description = null, amount = null;

            foreach (var cell in sheet.Row(rowNumber).CellsUsed())
            {
                var title = CellReader.Normalize(CellReader.Text(cell));
                var column = cell.Address.ColumnNumber;

                if (title == "data") date ??= column;
                else if (title == "descricao") description ??= column;
                else if (title.StartsWith("valor", StringComparison.Ordinal)) amount ??= column;
            }

            if (date is not null && description is not null && amount is not null)
                return (rowNumber, date.Value, description.Value, amount.Value);
        }

        throw new SpreadsheetFormatException(fileName,
            $"\"{fileName}\" não parece ser um extrato: as colunas \"Data\", \"Descrição\" e \"Valor\" não foram encontradas.");
    }
}
