namespace EventFlow.Application.Common;

public enum ErrorType { Validation, BusinessRule, NotFound, Unauthorized, Forbidden, Conflict }

public sealed record Error(ErrorType Type, string Message, IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error Validation(string message, IReadOnlyDictionary<string, string[]>? fields = null) => new(ErrorType.Validation, message, fields);
    public static Error Rule(string message) => new(ErrorType.BusinessRule, message);
    public static Error NotFound(string message) => new(ErrorType.NotFound, message);
    public static Error Unauthorized(string message = "Faça login para continuar.") => new(ErrorType.Unauthorized, message);
    public static Error Forbidden(string message = "Você não tem permissão para esta ação.") => new(ErrorType.Forbidden, message);
    public static Error Conflict(string message) => new(ErrorType.Conflict, message);
}

/// <summary>Resultado de um caso de uso: sucesso com valor ou falha com <see cref="Error"/>.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Resultado com erro não tem valor.");

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => Fail(error);
}

/// <summary>Valor vazio para comandos que não retornam dados.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
