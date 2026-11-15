using ClosedXML.Excel;

namespace Conciliador.TestData;

public sealed record SampleSale(
    DateOnly SaleDate,
    TimeOnly Time,
    string Product,
    string Installment,
    string CardType,
    string Brand,
    decimal Gross,
    decimal FeeRate,
    DateOnly? ExpectedPayment,
    DateOnly? EffectivePayment)
{
    public decimal Fee => Math.Round(Gross * FeeRate, 2, MidpointRounding.AwayFromZero);
    public decimal Net => Gross - Fee;
}

// Monta uma planilha ficticia no mesmo layout do "Relatorio Detalhado de Vendas" da maquininha:
// quatro linhas de titulo, cabecalho na linha 5 e 28 colunas
public static class SalesReportWorkbook
{
    public static readonly string[] Headers =
    {
        "Data da venda", "Hora da venda", "Código de autorização", "Código do estabelecimento",
        "Nome do estabelecimento", "Comprovante de venda", "Código do pedido", "Canal", "Número do Terminal",
        "Produto", "Parcelas", "Tipo de cartão", "Bandeira", "Status", "Valor bruto da transação",
        "Valor bruto da parcela", "Percentual da taxa (MDR)", "Valor da taxa (MDR)",
        "Valor líquido da parcela/transação", "Descrição (Order ID)", "Número do cartão", "Cartão pré pago",
        "Data prevista de pagamento da venda", "Status do pagamento da venda",
        "Data efetiva do pagamento da venda", "Código de pagamento", "Cód. Ref. Cartão", "Tipo de cartão"
    };

    public static byte[] Build(DateOnly periodStart, DateOnly periodEnd, IEnumerable<SampleSale> sales,
        string merchantName = "MERCADO EXEMPLO", string merchantCode = "10000001")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");

        sheet.Cell(1, 1).Value = "Relatório de Vendas.";
        sheet.Cell(2, 1).Value = $"Período selecionado:  {periodStart:dd/MM/yyyy} à {periodEnd:dd/MM/yyyy}";
        sheet.Cell(3, 1).Value = $"Emitido em:  {periodEnd.AddDays(10):dd/MM/yyyy} 08:00:00";
        sheet.Cell(4, 1).Value = $"Documento/EC :  {merchantCode}";

        for (var i = 0; i < Headers.Length; i++)
            sheet.Cell(5, i + 1).Value = Headers[i];

        var row = 6;
        var sequence = 1;
        foreach (var sale in sales)
        {
            sheet.Cell(row, 1).Value = sale.SaleDate.ToString("dd/MM/yyyy");
            sheet.Cell(row, 2).Value = sale.Time.ToString("HH:mm:ss");
            sheet.Cell(row, 3).Value = (100000 + sequence).ToString();
            sheet.Cell(row, 4).Value = merchantCode;
            sheet.Cell(row, 5).Value = merchantName;
            sheet.Cell(row, 6).Value = sequence.ToString("D9");
            sheet.Cell(row, 7).Value = (60000000000L + sequence).ToString();
            sheet.Cell(row, 8).Value = "TEF IP";
            sheet.Cell(row, 9).Value = "TERM0001";
            sheet.Cell(row, 10).Value = sale.Product;
            sheet.Cell(row, 11).Value = sale.Installment;
            sheet.Cell(row, 12).Value = sale.CardType;
            sheet.Cell(row, 13).Value = sale.Brand;
            sheet.Cell(row, 14).Value = "Aprovada";
            sheet.Cell(row, 15).Value = sale.Gross;
            sheet.Cell(row, 16).Value = sale.Gross;
            sheet.Cell(row, 17).Value = sale.FeeRate;
            sheet.Cell(row, 18).Value = sale.Fee;
            sheet.Cell(row, 19).Value = sale.Net;
            sheet.Cell(row, 20).Value = "-";
            sheet.Cell(row, 21).Value = "000000******0000";
            sheet.Cell(row, 22).Value = "Não";
            SetDate(sheet.Cell(row, 23), sale.ExpectedPayment);
            sheet.Cell(row, 24).Value = sale.EffectivePayment is null ? "A Pagar" : "Paga";
            SetDate(sheet.Cell(row, 25), sale.EffectivePayment);
            sheet.Cell(row, 26).Value = sale.EffectivePayment is null ? "-" : (61000000000L + sequence).ToString();
            sheet.Cell(row, 27).Value = "-";
            sheet.Cell(row, 28).Value = "Nacional";
            row++;
            sequence++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetDate(IXLCell cell, DateOnly? date)
    {
        if (date is null)
        {
            cell.Value = "-";
            return;
        }

        cell.Value = date.Value.ToDateTime(TimeOnly.MinValue);
        cell.Style.DateFormat.Format = "dd/MM/yyyy";
    }
}
