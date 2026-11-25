using System.Text.Json;

namespace EventFlow.Api.Infrastructure;

/// <summary>Lê enums escritos em snake_case na query string ("awaiting_payment" → OrderStatus.AwaitingPayment).</summary>
internal static class EnumCodes
{
    public static string Code<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>Null/vazio → (true, null); código válido → (true, valor); inválido → (false, null).</summary>
    public static bool TryParse<TEnum>(string? code, out TEnum? value) where TEnum : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(code)) return true;
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (Code(candidate) != code.Trim()) continue;
            value = candidate;
            return true;
        }
        return false;
    }

    public static IResult Invalid(string parameter) =>
        Results.Problem(detail: $"Valor inválido para '{parameter}'.", statusCode: StatusCodes.Status400BadRequest, title: "Dados inválidos");
}
