using Conciliador.Application.Abstractions;
using Conciliador.Domain.Reconciliations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Conciliador.Application.Reconciliations;

public sealed record UploadedFile(string FileName, long Length, Func<Stream> OpenRead);

public sealed record RunReconciliationRequest(
    OperationType? Operation,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<UploadedFile> SalesFiles,
    IReadOnlyList<UploadedFile> StatementFiles);

public sealed class RunReconciliationRequestValidator : AbstractValidator<RunReconciliationRequest>
{
    public const int MaxFilesPerSide = 12;
    public const long MaxFileSizeInBytes = 20 * 1024 * 1024;

    public RunReconciliationRequestValidator()
    {
        RuleFor(x => x.Operation)
            .NotNull().WithMessage("Informe a operação: débito ou crédito.")
            .Must(o => o is null || Enum.IsDefined(o.Value)).WithMessage("Operação inválida.");

        RuleFor(x => x.StartDate).NotNull().WithMessage("Informe a data inicial de pagamento.");
        RuleFor(x => x.EndDate).NotNull().WithMessage("Informe a data final de pagamento.");

        RuleFor(x => x)
            .Must(x => x.EndDate >= x.StartDate)
            .When(x => x.StartDate is not null && x.EndDate is not null)
            .WithName(nameof(RunReconciliationRequest.EndDate))
            .WithMessage("A data final não pode ser anterior à data inicial.");

        RuleFor(x => x)
            .Must(x => x.EndDate!.Value.DayNumber - x.StartDate!.Value.DayNumber + 1 <= Reconciliation.MaxRangeInDays)
            .When(x => x.StartDate is not null && x.EndDate is not null && x.EndDate >= x.StartDate)
            .WithName(nameof(RunReconciliationRequest.EndDate))
            .WithMessage($"O intervalo não pode passar de {Reconciliation.MaxRangeInDays} dias.");

        RuleFor(x => x.SalesFiles).Custom((files, context) => ValidateFiles(files, "relatório de vendas", context));
        RuleFor(x => x.StatementFiles).Custom((files, context) => ValidateFiles(files, "extrato", context));
    }

    private static void ValidateFiles<T>(IReadOnlyList<UploadedFile>? files, string label, ValidationContext<T> context)
    {
        if (files is null || files.Count == 0)
        {
            context.AddFailure($"Envie pelo menos um arquivo de {label}.");
            return;
        }

        if (files.Count > MaxFilesPerSide)
            context.AddFailure($"Envie no máximo {MaxFilesPerSide} arquivos de {label}.");

        foreach (var file in files)
        {
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                context.AddFailure($"O arquivo \"{file.FileName}\" não é .xlsx.");
            else if (file.Length == 0)
                context.AddFailure($"O arquivo \"{file.FileName}\" está vazio.");
            else if (file.Length > MaxFileSizeInBytes)
                context.AddFailure($"O arquivo \"{file.FileName}\" passa de {MaxFileSizeInBytes / 1024 / 1024} MB.");
        }
    }
}

public sealed class RunReconciliationUseCase
{
    private readonly ISalesReportParser _salesParser;
    private readonly IBankStatementParser _statementParser;
    private readonly IReconciliationRepository _repository;
    private readonly IValidator<RunReconciliationRequest> _validator;
    private readonly ReconciliationEngine _engine;
    private readonly IClock _clock;
    private readonly ILogger<RunReconciliationUseCase> _logger;

    public RunReconciliationUseCase(
        ISalesReportParser salesParser,
        IBankStatementParser statementParser,
        IReconciliationRepository repository,
        IValidator<RunReconciliationRequest> validator,
        ReconciliationEngine engine,
        IClock clock,
        ILogger<RunReconciliationUseCase> logger)
    {
        _salesParser = salesParser;
        _statementParser = statementParser;
        _repository = repository;
        _validator = validator;
        _engine = engine;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReconciliationDetailDto> ExecuteAsync(
        RunReconciliationRequest request, string userEmail, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var salesReports = new List<SalesReport>();
        foreach (var file in request.SalesFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = file.OpenRead();
            salesReports.Add(_salesParser.Parse(stream, file.FileName));
        }

        var statements = new List<BankStatement>();
        foreach (var file in request.StatementFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = file.OpenRead();
            statements.Add(_statementParser.Parse(stream, file.FileName));
        }

        var reconciliation = _engine.Reconcile(
            request.Operation!.Value, request.StartDate!.Value, request.EndDate!.Value,
            salesReports, statements, userEmail, _clock.UtcNow);

        await _repository.AddAsync(reconciliation, cancellationToken);

        _logger.LogInformation(
            "Reconciliation {Id} created: {Operation} {Start}..{End}, {Matched} matched, {Divergent} divergent",
            reconciliation.Id, reconciliation.Operation, reconciliation.StartDate, reconciliation.EndDate,
            reconciliation.MatchedLines, reconciliation.DivergentLines);

        return reconciliation.ToDetail();
    }
}
