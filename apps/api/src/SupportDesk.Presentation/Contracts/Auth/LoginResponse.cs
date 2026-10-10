
namespace SupportDesk.Presentation.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedAgentDto Agent);

public sealed record AuthenticatedAgentDto(
    int Id,
    string FullName,
    string Email);
