using EventFlow.Api.Infrastructure;
using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth;

namespace EventFlow.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public sealed record RefreshBody(string RefreshToken);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterCommand cmd, IDispatcher d, CancellationToken ct) =>
                (await d.Send(cmd, ct)).ToHttp())
            .RequireRateLimiting(RateLimitPolicy)
            .WithSummary("Cria uma conta de cliente e já devolve a sessão");

        group.MapPost("/login", async (LoginCommand cmd, IDispatcher d, CancellationToken ct) =>
                (await d.Send(cmd, ct)).ToHttp())
            .RequireRateLimiting(RateLimitPolicy)
            .WithSummary("Entra com e-mail e senha");

        group.MapPost("/refresh", async (RefreshBody body, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new RefreshCommand(body.RefreshToken), ct)).ToHttp())
            .RequireRateLimiting(RateLimitPolicy)
            .WithSummary("Troca o refresh token por uma nova sessão");

        group.MapPost("/logout", async (RefreshBody body, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new LogoutCommand(body.RefreshToken), ct)).ToNoContent())
            .WithSummary("Encerra a sessão (revoga o refresh token)");

        group.MapGet("/me", async (IDispatcher d, CancellationToken ct) => (await d.Send(new MeQuery(), ct)).ToHttp())
            .RequireAuthorization()
            .WithSummary("Usuário logado");
    }
}
