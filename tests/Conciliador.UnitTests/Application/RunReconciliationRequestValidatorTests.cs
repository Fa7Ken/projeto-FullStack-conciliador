using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using FluentAssertions;

namespace Conciliador.UnitTests.Application;

public class RunReconciliationRequestValidatorTests
{
    private readonly RunReconciliationRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_request()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Requires_operation_and_both_dates()
    {
        var result = _validator.Validate(Valid() with { Operation = null, StartDate = null, EndDate = null });

        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void Rejects_end_date_before_start_date()
    {
        var result = _validator.Validate(Valid() with { StartDate = new(2026, 3, 31), EndDate = new(2026, 3, 1) });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("anterior");
    }

    [Fact]
    public void Rejects_ranges_longer_than_the_limit()
    {
        var start = new DateOnly(2025, 1, 1);
        var result = _validator.Validate(Valid() with { StartDate = start, EndDate = start.AddDays(Reconciliation.MaxRangeInDays) });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("366");
    }

    [Fact]
    public void Requires_at_least_one_file_per_side()
    {
        var result = _validator.Validate(Valid() with { SalesFiles = Array.Empty<UploadedFile>(), StatementFiles = Array.Empty<UploadedFile>() });

        result.Errors.Select(e => e.ErrorMessage).Should().Contain(new[]
        {
            "Envie pelo menos um arquivo de relatório de vendas.",
            "Envie pelo menos um arquivo de extrato."
        });
    }

    [Fact]
    public void Rejects_wrong_extension_empty_and_oversized_files()
    {
        var files = new[]
        {
            File("vendas.csv", 10),
            File("vazio.xlsx", 0),
            File("grande.xlsx", RunReconciliationRequestValidator.MaxFileSizeInBytes + 1)
        };

        var result = _validator.Validate(Valid() with { SalesFiles = files });

        result.Errors.Should().HaveCount(3);
    }

    private static RunReconciliationRequest Valid() => new(
        OperationType.Debit, new(2026, 3, 1), new(2026, 3, 31),
        new[] { File("vendas.xlsx", 100) }, new[] { File("extrato.xlsx", 100) });

    private static UploadedFile File(string name, long length) => new(name, length, () => Stream.Null);
}
