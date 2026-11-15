namespace Conciliador.TestData;

// Cenario ficticio e deterministico: dois meses de vendas e o extrato do segundo mes.
// Nenhum dado real de cliente e usado; os valores saem de um gerador com semente fixa.
public sealed class SampleScenario
{
    public const decimal PosRentalDiscount = 150.00m;
    public const decimal InstallmentRemnant = 37.90m;

    public static readonly DateOnly FebruaryStart = new(2026, 2, 1);
    public static readonly DateOnly FebruaryEnd = new(2026, 2, 28);
    public static readonly DateOnly MarchStart = new(2026, 3, 1);
    public static readonly DateOnly MarchEnd = new(2026, 3, 31);

    // Data em que os relatorios "foram emitidos": pagamentos depois dela ainda estao "A Pagar"
    private static readonly DateOnly IssuedOn = new(2026, 4, 10);

    private SampleScenario(IReadOnlyList<SampleSale> february, IReadOnlyList<SampleSale> march,
        IReadOnlyList<SampleStatementRow> statement, DateOnly missingCreditDay, DateOnly posRentalDay, DateOnly remnantDay)
    {
        FebruarySales = february;
        MarchSales = march;
        MarchStatement = statement;
        MissingCreditVisaDay = missingCreditDay;
        PosRentalDay = posRentalDay;
        RemnantDay = remnantDay;
    }

    public IReadOnlyList<SampleSale> FebruarySales { get; }
    public IReadOnlyList<SampleSale> MarchSales { get; }
    public IReadOnlyList<SampleStatementRow> MarchStatement { get; }

    // Divergencias plantadas de proposito, para a demonstracao nao ser so "tudo bate"
    public DateOnly MissingCreditVisaDay { get; }
    public DateOnly PosRentalDay { get; }
    public DateOnly RemnantDay { get; }

    public byte[] FebruarySalesWorkbook() => SalesReportWorkbook.Build(FebruaryStart, FebruaryEnd, FebruarySales);
    public byte[] MarchSalesWorkbook() => SalesReportWorkbook.Build(MarchStart, MarchEnd, MarchSales);
    public byte[] MarchStatementWorkbook() => StatementWorkbook.Build(MarchStart, MarchEnd, MarchStatement);

    public static SampleScenario Create(int salesPerDay = 30, int seed = 2026)
    {
        var random = new Random(seed);
        var february = GenerateSales(random, FebruaryStart, FebruaryEnd, salesPerDay);
        var march = GenerateSales(random, MarchStart, MarchEnd, salesPerDay);

        var settlements = february.Concat(march)
            .Where(s => s.EffectivePayment is { } paid && paid >= MarchStart && paid <= MarchEnd)
            .GroupBy(s => (Date: s.EffectivePayment!.Value, s.CardType, s.Brand))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Net));

        var posRentalDay = settlements.Keys
            .Where(k => k.CardType == "Débito" && k.Brand == "Mastercard").Min(k => k.Date);
        var missingCreditDay = settlements.Keys
            .Where(k => k.CardType == "Crédito" && k.Brand == "Visa" && k.Date.Day >= 15).Min(k => k.Date);
        var remnantDay = settlements.Keys
            .Where(k => k.CardType == "Crédito" && k.Brand == "ELO" && k.Date.Day >= 20).Min(k => k.Date);

        var rows = new List<SampleStatementRow>();
        var document = 800000000;

        foreach (var (key, net) in settlements)
        {
            var isDebit = key.CardType == "Débito";
            var amount = net;

            // 1) Aluguel da maquininha descontado do primeiro repasse de debito Mastercard do mes
            if (isDebit && key.Brand == "Mastercard" && key.Date == posRentalDay) amount -= PosRentalDiscount;

            // 2) Um dia de credito Visa que nao aparece no extrato
            if (!isDebit && key.Brand == "Visa" && key.Date == missingCreditDay) continue;

            // 3) Parcela de uma venda de janeiro, cujo relatorio nao faz parte do cenario
            if (!isDebit && key.Brand == "ELO" && key.Date == remnantDay) amount += InstallmentRemnant;

            var statementBrand = key.Brand == "Mastercard" ? "MASTER" : key.Brand.ToUpperInvariant();
            rows.Add(new SampleStatementRow(key.Date,
                StatementWorkbook.CardDescription(isDebit ? "DEBITO" : "CREDITO", statementBrand),
                (document++).ToString(), amount));
        }

        // Ruido: o extrato real tem muito mais PIX e boleto do que repasse de cartao
        for (var day = MarchStart; day <= MarchEnd; day = day.AddDays(1))
        {
            if (!IsBusinessDay(day)) continue;
            for (var i = 0; i < 6; i++)
            {
                rows.Add(new SampleStatementRow(day, $"RECEBIMENTO PIX 000000000{i:D2} CLIENTE EXEMPLO {i + 1}", "PIX_CRED",
                    Money(random, 5, 180)));
            }

            rows.Add(new SampleStatementRow(day, "LIQUIDACAO BOLETO 00000000000100 FORNECEDOR EXEMPLO", " ", -Money(random, 200, 1500)));
        }

        return new SampleScenario(february, march, rows, missingCreditDay, posRentalDay, remnantDay);
    }

    private static List<SampleSale> GenerateSales(Random random, DateOnly start, DateOnly end, int salesPerDay)
    {
        var sales = new List<SampleSale>();

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            for (var i = 0; i < salesPerDay; i++)
            {
                var time = new TimeOnly(7, 0).AddMinutes(i * (14 * 60 / salesPerDay) + random.Next(0, 5));
                var gross = Money(random, 4, 220);
                var brand = PickBrand(random);
                var kind = random.Next(100);

                if (kind < 75)
                {
                    var rate = brand == "ELO" ? 0.0245m : 0.0077m;
                    sales.Add(Sale(day, time, "Débito", "-", "Débito", brand, gross, rate, NextBusinessDay(day.AddDays(1))));
                }
                else if (kind < 97)
                {
                    // Amex so existe no credito e e raro, como nos dados reais
                    if (random.Next(100) < 3) brand = "Amex";
                    sales.Add(Sale(day, time, "Crédito a Vista", "-", "Crédito", brand, gross, CreditRate(brand),
                        NextBusinessDay(day.AddDays(30))));
                }
                else
                {
                    // Parcelado em 2x: uma linha por parcela, cada uma com sua data de pagamento
                    var installment = Math.Round(gross * 2, 2);
                    for (var n = 1; n <= 2; n++)
                    {
                        sales.Add(Sale(day, time, "Parcelado Lojista", $"{n} de 2", "Crédito", brand, installment, 0.0199m,
                            NextBusinessDay(day.AddDays(30 * n))));
                    }
                }
            }
        }

        return sales;
    }

    private static SampleSale Sale(DateOnly day, TimeOnly time, string product, string installment, string cardType,
        string brand, decimal gross, decimal rate, DateOnly payment) =>
        new(day, time, product, installment, cardType, brand, gross, rate, payment, payment <= IssuedOn ? payment : null);

    private static string PickBrand(Random random)
    {
        var roll = random.Next(100);
        return roll < 52 ? "Mastercard" : roll < 82 ? "Visa" : "ELO";
    }

    private static decimal CreditRate(string brand) => brand is "ELO" or "Amex" ? 0.0345m : 0.0158m;

    private static decimal Money(Random random, int min, int max) =>
        Math.Round((decimal)(min + random.NextDouble() * (max - min)), 2);

    private static bool IsBusinessDay(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static DateOnly NextBusinessDay(DateOnly day)
    {
        while (!IsBusinessDay(day)) day = day.AddDays(1);
        return day;
    }
}
