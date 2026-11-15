using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Conciliador.Infrastructure.Spreadsheets;

// Leitura tolerante de celulas: as planilhas misturam datas reais, datas em texto e "-"
internal static class CellReader
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd" };

    public static string Text(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank) return string.Empty;
        if (value.IsText) return value.GetText().Trim();
        return cell.GetFormattedString().Trim();
    }

    // Remove acentos e padroniza espacos e caixa, para comparar titulos de coluna
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    public static bool TryDate(IXLCell cell, out DateOnly date)
    {
        date = default;
        var value = cell.Value;

        if (value.IsDateTime)
        {
            date = DateOnly.FromDateTime(value.GetDateTime());
            return true;
        }

        if (value.IsText)
        {
            return DateOnly.TryParseExact(value.GetText().Trim(), DateFormats, PtBr, DateTimeStyles.None, out date);
        }

        if (value.IsNumber)
        {
            // Data gravada como numero serial do Excel, sem formato de data
            var serial = value.GetNumber();
            if (serial is < 1 or > 2958465) return false;
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }

        return false;
    }

    public static bool TryDecimal(IXLCell cell, out decimal amount)
    {
        amount = 0m;
        var value = cell.Value;

        if (value.IsNumber)
        {
            amount = Math.Round((decimal)value.GetNumber(), 2, MidpointRounding.AwayFromZero);
            return true;
        }

        if (value.IsText)
        {
            var text = value.GetText().Replace("R$", string.Empty).Trim();
            return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, PtBr, out amount);
        }

        return false;
    }
}
