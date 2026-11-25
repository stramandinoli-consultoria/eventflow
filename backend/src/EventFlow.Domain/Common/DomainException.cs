namespace EventFlow.Domain.Common;

/// <summary>Violação de uma regra de negócio. A API converte em resposta 422.</summary>
public sealed class DomainException(string message) : Exception(message);
