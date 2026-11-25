using EventFlow.Application.Common;

namespace EventFlow.Api.Infrastructure;

/// <summary>Converte o <see cref="Result{T}"/> dos casos de uso em resposta HTTP (ProblemDetails nos erros).</summary>
internal static class ResultHttp
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess) return onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value);
        return Problem(result.Error!);
    }

    public static IResult ToNoContent<T>(this Result<T> result) => result.IsSuccess ? Results.NoContent() : Problem(result.Error!);

    public static IResult Problem(Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Dados inválidos"),
            ErrorType.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "Operação não permitida"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Não encontrado"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Sem permissão"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflito"),
            _ => (StatusCodes.Status400BadRequest, "Erro"),
        };

        var extensions = error.Fields is null ? null : new Dictionary<string, object?> { ["errors"] = error.Fields };
        return Results.Problem(detail: error.Message, statusCode: status, title: title, extensions: extensions);
    }
}
