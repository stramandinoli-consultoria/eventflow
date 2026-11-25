namespace EventFlow.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength = 200)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new DomainException($"{field} é obrigatório.");
        if (trimmed.Length > maxLength) throw new DomainException($"{field} deve ter no máximo {maxLength} caracteres.");
        return trimmed;
    }

    public static string Optional(string? value, string field, int maxLength = 2000)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength) throw new DomainException($"{field} deve ter no máximo {maxLength} caracteres.");
        return trimmed;
    }
}
