
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(
            sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        // Use ASP.NET Core's vetted password hashing implementation.
        services.AddScoped<IPasswordHasher<Agent>, PasswordHasher<Agent>>();

        // Register SLA configuration from appsettings.json.
        services.Configure<SlaOptions>(
            configuration.GetSection(SlaOptions.SectionName));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Ticket triage
        services.AddScoped<TicketTriageService>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();
        services.AddScoped<ITicketEscalationRepository, TicketEscalationRepository>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}
