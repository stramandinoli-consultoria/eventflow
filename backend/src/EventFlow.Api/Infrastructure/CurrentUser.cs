using System.Security.Claims;
using System.Text.Json;
using EventFlow.Application.Abstractions;
using EventFlow.Domain;
using EventFlow.Infrastructure.Security;

namespace EventFlow.Api.Infrastructure;

/// <summary>Lê o usuário logado a partir das claims do JWT da requisição.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? Id => Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirstValue(AppClaims.Subject) : null;
    public string? Email => Id is null ? null : Principal!.FindFirstValue(AppClaims.Email);
    public string? PartnerId => Id is null ? null : Principal!.FindFirstValue(AppClaims.PartnerId);

    public UserRole? Role
    {
        get
        {
            var code = Id is null ? null : Principal!.FindFirstValue(AppClaims.Role);
            if (code is null) return null;
            return Enum.GetValues<UserRole>()
                .Cast<UserRole?>()
                .FirstOrDefault(r => JsonNamingPolicy.SnakeCaseLower.ConvertName(r!.Value.ToString()) == code);
        }
    }
}
