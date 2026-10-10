
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Contracts.Auth;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    SupportDbContext db,
    IPasswordHasher<Agent> passwordHasher,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Email == request.Email.Trim(),
                ct);

        // Use the same response for an unknown email and an incorrect password.
        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verification = passwordHasher.VerifyHashedPassword(
            agent,
            agent.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");

        var expiryMinutes = configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 60;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new LoginResponse(
            accessToken,
            expiresAtUtc,
            new AuthenticatedAgentDto(agent.Id, agent.FullName, agent.Email)));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedAgentDto>> Me(CancellationToken ct)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idValue, out var agentId))
        {
            return Unauthorized();
        }

        var agent = await db.Set<Agent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedAgentDto(
            agent.Id,
            agent.FullName,
            agent.Email));
    }
}
