
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Services;

public sealed class TicketTriageService(
    IAgentRepository agents,
    ITicketRepository tickets,
    IOptions<SlaOptions> slaOptions,
    IClock clock)
{
    public async Task<TriageDto> ApplyAsync(
        Ticket ticket,
        Customer customer,
        Category category,
        CancellationToken ct)
    {
        var priorityReason = "The requested priority was used.";

        // Categories that require critical handling override the requested priority.
        if (category.ForcesCriticalPriority)
        {
            ticket.SetPriority(TicketPriority.Critical, clock.UtcNow);
            priorityReason = "The category requires Critical priority.";
        }
        else
        {
            priorityReason =
                $"Priority set to {ticket.Priority}, using the requested priority or Medium by default.";
        }

        var settings = slaOptions.Value;

        if (!settings.BaseWindowHours.TryGetValue(
                ticket.Priority.ToString(), out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority {ticket.Priority}.");
        }

        var multiplier = customer.Tier == CustomerTier.Premium
            ? settings.PremiumCustomerMultiplier
            : 1.0;

        var windowHours = Math.Max(
            settings.MinimumWindowHours,
            baseHours * multiplier);

        var dueAtUtc = clock.UtcNow.AddHours(windowHours);
        ticket.SetDueAt(dueAtUtc, clock.UtcNow);

        var slaReason = customer.Tier == CustomerTier.Premium
            ? $"Premium customer SLA: {windowHours:0.##} hours."
            : $"Standard SLA: {windowHours:0.##} hours.";

        var candidates = await agents.GetAllForAssignmentAsync(ct);
        var eligible = new List<(Agent Agent, int OpenCount)>();

        foreach (var agent in candidates)
        {
            if (!agent.IsActive)
            {
                continue;
            }

            if (category.RequiresSpecialist &&
                !agent.Specializations.Any(s => s.CategoryId == category.Id))
            {
                continue;
            }

            var openCount = await tickets.CountOpenByAgentAsync(agent.Id, ct);

            // An agent at capacity is not eligible.
            if (openCount >= agent.MaxOpenTickets)
            {
                continue;
            }

            eligible.Add((agent, openCount));
        }

        var selected = eligible
            .OrderBy(candidate => candidate.OpenCount)
            .ThenBy(candidate => candidate.Agent.FullName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Agent.Id)
            .FirstOrDefault();

        string assignmentReason;

        if (selected.Agent is null)
        {
            ticket.AssignTo(null, clock.UtcNow);

            assignmentReason = category.RequiresSpecialist
                ? "No active specialist with available capacity was found. The ticket remains unassigned."
                : "No active agent with available capacity was found. The ticket remains unassigned.";
        }
        else
        {
            ticket.AssignTo(selected.Agent.Id, clock.UtcNow);

            assignmentReason =
                $"Assigned to {selected.Agent.FullName}, who has the fewest open tickets among eligible agents.";
        }

        return new TriageDto
        {
            PriorityReason = priorityReason,
            SlaReason = slaReason,
            AssignmentReason = assignmentReason
        };
    }
}
