using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace Conciliador.Infrastructure.Spreadsheets;

// Extrai o periodo declarado no topo do arquivo, ex.: "Periodo selecionado: 01/03/2026 a 31/03/2026"
internal static partial class PeriodReader
{
    [GeneratedRegex(@"(\d{2}/\d{2}/\d{4})\s*(?:a|à|ate|até|-)\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PeriodRegex();

    public static (DateOnly? Start, DateOnly? End) Read(IXLWorksheet sheet, int headerRow)
    {
        for (var rowNumber = 1; rowNumber < headerRow; rowNumber++)
        {
            foreach (var cell in sheet.Row(rowNumber).CellsUsed())
            {
                var match = PeriodRegex().Match(CellReader.Text(cell));
                if (!match.Success) continue;

                var culture = CultureInfo.InvariantCulture;
                if (DateOnly.TryParseExact(match.Groups[1].Value, "dd/MM/yyyy", culture, DateTimeStyles.None, out var start) &&
                    DateOnly.TryParseExact(match.Groups[2].Value, "dd/MM/yyyy", culture, DateTimeStyles.None, out var end))
                {
                    return (start, end);
                }
            }
        }

        return (null, null);
    }
}
