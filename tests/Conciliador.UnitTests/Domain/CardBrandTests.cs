using Conciliador.Domain.Reconciliations;
using FluentAssertions;

namespace Conciliador.UnitTests.Domain;

public class CardBrandTests
{
    [Theory]
    [InlineData("MASTER", "Mastercard")]
    [InlineData("Mastercard", "Mastercard")]
    [InlineData(" visa ", "Visa")]
    [InlineData("ELO", "Elo")]
    [InlineData("amex", "Amex")]
    [InlineData("CABAL", "Cabal")]
    public void Normalizes_statement_and_report_spellings_to_the_same_name(string raw, string expected)
    {
        CardBrand.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_brand(string? raw)
    {
        var act = () => CardBrand.Normalize(raw);
        act.Should().Throw<ArgumentException>();
    }
}
