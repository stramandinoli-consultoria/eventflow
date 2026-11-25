using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Auth;

public sealed record RegisterCommand(string Name, string Email, string Password, string? Phone) : ICommand<AuthResponse>;
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;
public sealed record RefreshCommand(string RefreshToken) : ICommand<AuthResponse>;
public sealed record LogoutCommand(string RefreshToken) : ICommand<Unit>;
public sealed record MeQuery : IQuery<UserDto>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Preencha todos os campos").MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().WithMessage("Preencha todos os campos").EmailAddress().WithMessage("E-mail inválido").MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Preencha todos os campos").MinimumLength(6).WithMessage("A senha deve ter pelo menos 6 caracteres").MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Credenciais inválidas");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Credenciais inválidas");
    }
}

/// <summary>Emite access token + refresh token (guardado só como hash).</summary>
public sealed class SessionIssuer(IAppDbContext db, ITokenService tokens, ICrypto crypto, TimeProvider clock)
{
    public AuthResponse Issue(User user)
    {
        var (access, expiresAt) = tokens.CreateAccessToken(user);
        var refresh = crypto.RandomToken(48);
        db.RefreshTokens.Add(RefreshToken.Issue(user.Id, crypto.Hash(refresh), clock.GetUtcNow().Add(tokens.RefreshTokenLifetime)));
        return new AuthResponse(access, expiresAt, refresh, user.ToDto());
    }
}

internal sealed class RegisterHandler(IAppDbContext db, IPasswordHasher hasher, SessionIssuer sessions, TimeProvider clock)
    : IRequestHandler<RegisterCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var email = User.NormalizeEmail(cmd.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Error.Conflict("Já existe uma conta com este e-mail.");

        // Cadastro público cria sempre cliente; parceiros e admins são criados pelo admin.
        var user = User.Create("u-" + Guid.NewGuid().ToString("N")[..12], cmd.Name, email, cmd.Phone,
            hasher.Hash(cmd.Password), UserRole.Customer, clock.GetUtcNow());
        db.Users.Add(user);
        var session = sessions.Issue(user);
        await db.SaveChangesAsync(ct);
        return session;
    }
}

internal sealed class LoginHandler(IAppDbContext db, IPasswordHasher hasher, SessionIssuer sessions)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !hasher.Verify(user.PasswordHash, cmd.Password))
            return Error.Unauthorized("Credenciais inválidas");

        var session = sessions.Issue(user);
        await db.SaveChangesAsync(ct);
        return session;
    }
}

internal sealed class RefreshHandler(IAppDbContext db, ICrypto crypto, SessionIssuer sessions, TimeProvider clock)
    : IRequestHandler<RefreshCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = crypto.Hash(cmd.RefreshToken ?? "");
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive(now)) return Error.Unauthorized("Sessão expirada. Entre novamente.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null) return Error.Unauthorized("Sessão expirada. Entre novamente.");

        stored.Revoke(now); // rotação: cada refresh token vale uma vez
        var session = sessions.Issue(user);
        await db.SaveChangesAsync(ct);
        return session;
    }
}

internal sealed class LogoutHandler(IAppDbContext db, ICrypto crypto, TimeProvider clock) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = crypto.Hash(cmd.RefreshToken ?? "");
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null)
        {
            stored.Revoke(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        return Unit.Value;
    }
}

internal sealed class MeHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<MeQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(MeQuery query, CancellationToken ct)
    {
        if (current.Id is null) return Error.Unauthorized();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        return user is null ? Error.Unauthorized() : user.ToDto();
    }
}
