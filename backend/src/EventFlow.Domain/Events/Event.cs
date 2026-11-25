using EventFlow.Domain.Common;

namespace EventFlow.Domain.Events;

public sealed class Event
{
    public string Id { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Banner { get; private set; } = default!;
    /// <summary>Data e hora local do evento (sem fuso), como o organizador digita.</summary>
    public DateTime StartsAt { get; private set; }
    public string Location { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public EventStatus Status { get; private set; }

    private Event() { }

    public static Event Create(string id, string name, DateTime startsAt, string location, string? description,
        string banner, EventStatus status)
    {
        var ev = new Event { Id = Guard.Required(id, "Id", 64) };
        ev.Update(name, startsAt, location, description, banner, status);
        return ev;
    }

    public void Update(string name, DateTime startsAt, string location, string? description, string banner, EventStatus status)
    {
        Name = Guard.Required(name, "Nome");
        StartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Unspecified);
        Location = Guard.Required(location, "Local");
        Description = Guard.Optional(description, "Descrição");
        Banner = Guard.Required(banner, "Banner", 2000);
        Status = status;
    }
}
