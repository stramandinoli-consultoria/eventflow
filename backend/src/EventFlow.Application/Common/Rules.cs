using FluentValidation;

namespace EventFlow.Application.Common;

/// <summary>Regras de validação reutilizadas pelos comandos.</summary>
public static class Rules
{
    /// <summary>Ids gerados pelo front: letras, números, "-" e "_".</summary>
    public static IRuleBuilderOptions<T, string> ValidId<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Id é obrigatório.")
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Id deve conter apenas letras, números, '-' ou '_'.");

    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => string.IsNullOrWhiteSpace(v) || (Uri.TryCreate(v, UriKind.Absolute, out var u)
                && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("Informe uma URL http(s) válida.");
}
