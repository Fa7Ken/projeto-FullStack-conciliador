using Conciliador.Application.Common;
using Conciliador.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Conciliador.Api.Infrastructure;

// Converte as excecoes conhecidas em respostas ProblemDetails com mensagem pronta para o usuario
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
                Detail = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage).Distinct())
            },
            DomainException domain => Problem(StatusCodes.Status400BadRequest, "Dados inválidos", domain.Message),
            SpreadsheetFormatException spreadsheet => WithFile(
                Problem(StatusCodes.Status422UnprocessableEntity, "Planilha fora do formato esperado", spreadsheet.Message),
                spreadsheet.FileName),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, "Não encontrado", notFound.Message),
            InvalidCredentialsException credentials => Problem(StatusCodes.Status401Unauthorized, "Não autorizado", credentials.Message),
            _ => null
        };

        if (problem is null)
        {
            // Erro inesperado: registra o detalhe no log e devolve uma mensagem generica
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Erro interno",
                "Ocorreu um erro inesperado. Tente novamente em instantes.");
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync<object>(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails WithFile(ProblemDetails problem, string fileName)
    {
        problem.Extensions["fileName"] = fileName;
        return problem;
    }
}
