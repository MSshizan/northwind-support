using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Contracts.Auth;

namespace SupportDesk.UnitTests.Integration;

public sealed class AuthControllerTests : IAsyncLifetime
{
    private const string TestSigningKey =
        "test-only-signing-key-for-auth-tests-32-characters-minimum";

    private const string TestPassword = "TestPassword!2026";

    private SqliteConnection _connection = null!;
    private AuthTestApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _factory = new AuthTestApplicationFactory(_connection);
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();

        await db.Database.EnsureCreatedAsync();

        var hasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<Agent>>();

        var activeAgent = new Agent(
            "Test Agent",
            "test.agent@example.com",
            5,
            DateTime.UtcNow);

        activeAgent.SetPasswordHash(
            hasher.HashPassword(activeAgent, TestPassword));

        var inactiveAgent = new Agent(
            "Inactive Agent",
            "inactive.agent@example.com",
            5,
            DateTime.UtcNow);

        inactiveAgent.SetPasswordHash(
            hasher.HashPassword(inactiveAgent, TestPassword));

        inactiveAgent.Deactivate();

        db.Set<Agent>().AddRange(activeAgent, inactiveAgent);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_WhenPasswordIsIncorrect_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test.agent@example.com",
                password = "WrongPassword!2026"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenEmailDoesNotExist_ReturnsSameUnauthorizedResponse()
    {
        var knownEmailResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test.agent@example.com",
                password = "WrongPassword!2026"
            });

        var unknownEmailResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "missing@example.com",
                password = "WrongPassword!2026"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            knownEmailResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            unknownEmailResponse.StatusCode);

        var knownBody =
            await knownEmailResponse.Content.ReadAsStringAsync();

        var unknownBody =
            await unknownEmailResponse.Content.ReadAsStringAsync();

        Assert.Equal(knownBody, unknownBody);
    }

    [Fact]
    public async Task Login_WhenAgentIsInactive_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "inactive.agent@example.com",
                password = TestPassword
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsTokenAndAgent()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test.agent@example.com",
                password = TestPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal("Test Agent", result.Agent.FullName);
        Assert.Equal("test.agent@example.com", result.Agent.Email);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsAuthenticatedAgent()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "test.agent@example.com",
                password = TestPassword
            });

        loginResponse.EnsureSuccessStatusCode();

        var login =
            await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var agent =
            await response.Content.ReadFromJsonAsync<AuthenticatedAgentDto>();

        Assert.NotNull(agent);
        Assert.Equal("Test Agent", agent.FullName);
        Assert.Equal("test.agent@example.com", agent.Email);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class AuthTestApplicationFactory(
        SqliteConnection connection)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SigningKey", TestSigningKey);
            builder.UseSetting("Jwt:ExpiryMinutes", "60");
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Data Source=auth-tests.db");

            builder.ConfigureServices(services =>
            {
                // Remove SQL Server options configuration for SupportDbContext.
                var databaseConfigurations = services
                    .Where(service =>
                        service.ServiceType.IsGenericType &&
                        service.ServiceType.Name.Contains(
                            "IDbContextOptionsConfiguration",
                            StringComparison.Ordinal) &&
                        service.ServiceType.GenericTypeArguments[0] ==
                            typeof(SupportDbContext))
                    .ToArray();

                foreach (var service in databaseConfigurations)
                {
                    services.Remove(service);
                }

                // Remove existing SupportDbContext registrations.
                services.RemoveAll<DbContextOptions<SupportDbContext>>();
                services.RemoveAll<SupportDbContext>();

                // Use SQLite only for these integration tests.
                services.AddDbContext<SupportDbContext>(options =>
                    options.UseSqlite(connection));
            });
        }
    }
}
