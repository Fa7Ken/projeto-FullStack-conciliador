using System.Globalization;

namespace Conciliador.Domain.Reconciliations;

// Padroniza o nome da bandeira, pois o extrato e o relatorio de vendas escrevem diferente
// (ex.: "MASTER" no extrato e "Mastercard" no relatorio)
public static class CardBrand
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MASTER"] = "Mastercard",
        ["MASTERCARD"] = "Mastercard",
        ["VISA"] = "Visa",
        ["ELO"] = "Elo",
        ["AMEX"] = "Amex",
        ["AMERICAN EXPRESS"] = "Amex",
        ["HIPER"] = "Hipercard",
        ["HIPERCARD"] = "Hipercard"
    };

    public static string Normalize(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new ArgumentException("Card brand cannot be empty.", nameof(raw));

        if (Aliases.TryGetValue(value, out var known))
            return known;

        // Bandeira desconhecida: mantem, apenas padronizando a escrita
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }
}
