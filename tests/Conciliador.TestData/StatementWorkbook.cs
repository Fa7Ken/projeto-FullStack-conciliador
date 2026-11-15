using ClosedXML.Excel;

namespace Conciliador.TestData;

public sealed record SampleStatementRow(DateOnly Date, string Description, string Document, decimal Amount);

// Monta um extrato ficticio no mesmo layout do banco: cabecalho na linha 9,
// lancamentos misturados (PIX, boletos, repasses de cartao) e bloco de lancamentos futuros no fim
public static class StatementWorkbook
{
    public const string Acquirer = "BANCOX";

    public static string CardDescription(string operation, string brand) =>
        $"{Acquirer} {operation} {brand} | {Acquirer} | 0001-00";

    public static byte[] Build(DateOnly periodStart, DateOnly periodEnd, IEnumerable<SampleStatementRow> rows,
        bool includeFutureEntries = true, string accountHolder = "MERCADO EXEMPLO LTDA")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Extrato");

        sheet.Cell(2, 1).Value = "Associado:";
        sheet.Cell(2, 2).Value = accountHolder;
        sheet.Cell(3, 1).Value = "Cooperativa:";
        sheet.Cell(3, 2).Value = "0000";
        sheet.Cell(4, 1).Value = "Conta:";
        sheet.Cell(4, 2).Value = "00000-0";
        sheet.Cell(6, 1).Value = "Extrato";
        sheet.Cell(7, 1).Value = $"Dados referentes ao período de {periodStart:dd/MM/yyyy} a {periodEnd:dd/MM/yyyy}.";

        sheet.Cell(9, 1).Value = "Data";
        sheet.Cell(9, 2).Value = "Descrição";
        sheet.Cell(9, 3).Value = "Documento";
        sheet.Cell(9, 4).Value = "Valor (R$)";
        sheet.Cell(9, 5).Value = "Saldo (R$)";

        var balance = 10000m;
        sheet.Cell(10, 2).Value = "Saldo Anterior";
        sheet.Cell(10, 5).Value = balance;

        var row = 11;
        foreach (var entry in rows.OrderBy(r => r.Date))
        {
            balance += entry.Amount;
            sheet.Cell(row, 1).Value = entry.Date.ToString("dd/MM/yyyy");
            sheet.Cell(row, 2).Value = entry.Description;
            sheet.Cell(row, 3).Value = entry.Document;
            sheet.Cell(row, 4).Value = entry.Amount;
            sheet.Cell(row, 5).Value = balance;
            row++;
        }

        if (includeFutureEntries)
        {
            row++;
            sheet.Cell(row++, 1).Value = "Lançamentos Futuros (Próximos 30 dias)";
            sheet.Cell(row, 1).Value = "Data";
            sheet.Cell(row, 2).Value = "Descrição";
            sheet.Cell(row++, 3).Value = "Valor (R$)";
            sheet.Cell(row, 1).Value = periodEnd.AddDays(15).ToString("dd/MM/yyyy");
            // Linha proposital: parece repasse de cartao, mas esta no bloco futuro e deve ser ignorada
            sheet.Cell(row, 2).Value = CardDescription("DEBITO", "VISA");
            sheet.Cell(row, 3).Value = 999.99m;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
