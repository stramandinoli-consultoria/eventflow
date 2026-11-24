using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Api.Infrastructure;

/// <summary>Erros inesperados viram ProblemDetails sem expor detalhes internos.</summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito",
                "Este registro foi alterado por outra pessoa. Atualize a tela e tente de novo."),
            BadHttpRequestException bad => (bad.StatusCode, "Requisição inválida", "Não foi possível ler os dados enviados."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Algo deu errado. Tente novamente em instantes."),
        };

        if (status >= 500) logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
