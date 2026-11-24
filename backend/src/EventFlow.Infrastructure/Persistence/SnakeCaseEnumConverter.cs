using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EventFlow.Infrastructure.Persistence;

/// <summary>Converte enum ↔ texto snake_case (OrderStatus.AwaitingPayment ↔ "awaiting_payment").</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToCode(value),
    code => FromCode(code))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Codes =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Codes.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToCode(TEnum value) => Codes[value];

    public static TEnum FromCode(string code) =>
        Values.TryGetValue(code, out var value)
            ? value
            : throw new InvalidOperationException($"Valor '{code}' inválido para {typeof(TEnum).Name}.");
}
