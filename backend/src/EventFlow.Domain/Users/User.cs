using EventFlow.Domain.Common;

namespace EventFlow.Domain.Users;

public sealed class User
{
    public string Id { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public string? PartnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { }

    public static User Create(string id, string name, string email, string? phone, string passwordHash,
        UserRole role, DateTimeOffset now, string? partnerId = null)
    {
        if (role == UserRole.Partner && string.IsNullOrWhiteSpace(partnerId))
            throw new DomainException("Usuário parceiro precisa estar vinculado a uma empresa.");

        return new User
        {
            Id = id,
            Name = Guard.Required(name, "Nome"),
            Email = NormalizeEmail(email),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            PartnerId = role == UserRole.Partner ? partnerId : null,
            CreatedAt = now,
        };
    }

    public static string NormalizeEmail(string email) => Guard.Required(email, "E-mail", 320).ToLowerInvariant();
}
