namespace Conciliador.Domain.Reconciliations;

// Arquivo enviado pelo usuario, com o periodo que ele declara cobrir
public sealed class SourceDocument
{
    private SourceDocument() { }

    public SourceDocument(string fileName, SourceKind kind, DateOnly? periodStart, DateOnly? periodEnd, int entryCount)
    {
        FileName = fileName;
        Kind = kind;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        EntryCount = entryCount;
    }

    public string FileName { get; private set; } = string.Empty;
    public SourceKind Kind { get; private set; }
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public int EntryCount { get; private set; }
}
