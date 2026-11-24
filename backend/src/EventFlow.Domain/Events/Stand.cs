using EventFlow.Domain.Common;

namespace EventFlow.Domain.Events;

/// <summary>
/// Elemento do mapa do evento. Posição e tamanho ficam em coordenadas lógicas
/// fixas; o front só aplica escala na tela (regra do AGENTS.md).
/// </summary>
public sealed class Stand
{
    public const int MinSize = 20;
    public const int MaxSize = 2000;
    public const int MaxCoordinate = 10000;

    public string Id { get; private set; } = default!;
    public string EventId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public StandType Category { get; private set; }
    public string Description { get; private set; } = default!;
    public string Cover { get; private set; } = default!;
    public decimal Rating { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    private Stand() { }

    public static Stand Create(string id, string eventId, string name, StandType category, string? description,
        string? cover, decimal rating, int x, int y, int width, int height)
    {
        var stand = new Stand { Id = Guard.Required(id, "Id", 64), EventId = Guard.Required(eventId, "Evento", 64) };
        stand.Update(name, category, description, cover, rating, width, height);
        stand.MoveTo(x, y);
        return stand;
    }

    public void Update(string name, StandType category, string? description, string? cover, decimal rating, int width, int height)
    {
        // O editor de mapa salva a cada tecla, então nome vazio é aceito enquanto o organizador digita.
        Name = Guard.Optional(name, "Nome", 120);
        Category = category;
        Description = Guard.Optional(description, "Descrição");
        Cover = Guard.Optional(cover, "Capa");
        if (rating is < 0 or > 5) throw new DomainException("A avaliação deve estar entre 0 e 5.");
        Rating = rating;
        Width = Math.Clamp(width, MinSize, MaxSize);
        Height = Math.Clamp(height, MinSize, MaxSize);
    }

    public void MoveTo(int x, int y)
    {
        X = Math.Clamp(x, 0, MaxCoordinate);
        Y = Math.Clamp(y, 0, MaxCoordinate);
    }
}
