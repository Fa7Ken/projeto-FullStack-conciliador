using Conciliador.TestData;

// Gera as planilhas ficticias da pasta samples/, usadas para demonstrar a ferramenta sem expor clientes.
// Uso: dotnet run --project tools/Conciliador.SampleGenerator [pasta-de-saida]
var output = args.Length > 0 ? args[0] : "samples";
Directory.CreateDirectory(output);

var scenario = SampleScenario.Create();

Write("relatorio-vendas-2026-02.xlsx", scenario.FebruarySalesWorkbook());
Write("relatorio-vendas-2026-03.xlsx", scenario.MarchSalesWorkbook());
Write("extrato-2026-03.xlsx", scenario.MarchStatementWorkbook());

Console.WriteLine($"POS rental discount on {scenario.PosRentalDay:dd/MM/yyyy} (debit Mastercard)");
Console.WriteLine($"Missing credit Visa settlement on {scenario.MissingCreditVisaDay:dd/MM/yyyy}");
Console.WriteLine($"Installment remnant on {scenario.RemnantDay:dd/MM/yyyy} (credit Elo)");

void Write(string name, byte[] content)
{
    var path = Path.Combine(output, name);
    File.WriteAllBytes(path, content);
    Console.WriteLine($"{path} ({content.Length / 1024} KB)");
}
