namespace EventFlow.Domain.Users;

/// <summary>Refresh token guardado só como hash; é trocado a cada uso (rotação).</summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(string userId, string tokenHash, DateTimeOffset expiresAt) =>
        new() { Id = Guid.NewGuid(), UserId = userId, TokenHash = tokenHash, ExpiresAt = expiresAt };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
