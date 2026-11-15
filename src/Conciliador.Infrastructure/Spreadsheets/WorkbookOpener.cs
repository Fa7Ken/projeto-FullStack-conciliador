using ClosedXML.Excel;
using Conciliador.Application.Common;

namespace Conciliador.Infrastructure.Spreadsheets;

internal static class WorkbookOpener
{
    public static XLWorkbook Open(Stream content, string fileName)
    {
        try
        {
            return new XLWorkbook(content);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new SpreadsheetFormatException(fileName,
                $"Não foi possível abrir \"{fileName}\". Confirme se é uma planilha .xlsx válida e sem senha.");
        }
    }
}
