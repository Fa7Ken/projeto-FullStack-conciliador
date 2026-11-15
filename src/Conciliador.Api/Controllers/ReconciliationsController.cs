using System.Security.Claims;
using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conciliador.Api.Controllers;

public sealed class RunReconciliationForm
{
    /// <summary>Debit or Credit.</summary>
    public OperationType? Operation { get; set; }

    /// <summary>First payment date (the day the money lands on the statement), yyyy-MM-dd.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Last payment date, yyyy-MM-dd.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>One or more card machine sales reports (.xlsx).</summary>
    public List<IFormFile> SalesFiles { get; set; } = new();

    /// <summary>One or more bank statements (.xlsx).</summary>
    public List<IFormFile> StatementFiles { get; set; } = new();
}

[ApiController]
[Authorize]
[Route("api/reconciliations")]
public sealed class ReconciliationsController : ControllerBase
{
    public const long MaxRequestSizeInBytes = 300L * 1024 * 1024;

    /// <summary>Reconciles sales reports against bank statements, day by day and per card brand.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeInBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeInBytes)]
    [ProducesResponseType(typeof(ReconciliationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReconciliationDetailDto>> Run(
        [FromForm] RunReconciliationForm form, [FromServices] RunReconciliationUseCase useCase, CancellationToken cancellationToken)
    {
        var request = new RunReconciliationRequest(
            form.Operation, form.StartDate, form.EndDate,
            form.SalesFiles.Select(ToUploadedFile).ToList(),
            form.StatementFiles.Select(ToUploadedFile).ToList());

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "unknown";
        var result = await useCase.ExecuteAsync(request, email, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Summary.Id }, result);
    }

    /// <summary>Lists past reconciliations, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReconciliationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReconciliationSummaryDto>>> List(
        [FromServices] ReconciliationQueries queries, CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await queries.ListAsync(page, pageSize, cancellationToken));
    }

    /// <summary>Returns one reconciliation with its lines, subtotals and issues.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReconciliationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconciliationDetailDto>> GetById(
        Guid id, [FromServices] ReconciliationQueries queries, CancellationToken cancellationToken)
    {
        return Ok(await queries.GetByIdAsync(id, cancellationToken));
    }

    // Usa so o nome do arquivo, descartando qualquer caminho enviado pelo navegador
    private static UploadedFile ToUploadedFile(IFormFile file) =>
        new(Path.GetFileName(file.FileName), file.Length, file.OpenReadStream);
}
