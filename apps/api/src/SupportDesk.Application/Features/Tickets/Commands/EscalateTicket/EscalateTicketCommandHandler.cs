
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ITicketEscalationRepository escalations,
    IUnitOfWork unitOfWork,
    IClock clock,
    IOptions<SlaOptions> slaOptions,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 500)
        {
            errors["Reason"] =
            [
                "The reason must contain between 5 and 500 characters."
            ];
        }

        if (string.IsNullOrWhiteSpace(request.EscalatedBy) ||
            request.EscalatedBy.Trim().Length > 100)
        {
            errors["EscalatedBy"] =
            [
                "The person requesting the escalation is required and must not exceed 100 characters."
            ];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException(
                "Resolved or closed tickets cannot be escalated.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                "This ticket is already at Critical priority and cannot be escalated further.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var previousPriority = ticket.Priority;
        var previousAgentId = ticket.AssignedAgentId;
        var previousDueAtUtc = ticket.DueAtUtc;
        var now = clock.UtcNow;

        var nextPriority = ticket.Priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => throw new ConflictException(
                "This ticket cannot be escalated further.")
        };

        var settings = slaOptions.Value;

        if (!settings.BaseWindowHours.TryGetValue(
                nextPriority.ToString(), out var baseHours))
        {
            throw new InvalidOperationException(
                $"No SLA window is configured for priority {nextPriority}.");
        }

        var multiplier = customer.Tier == CustomerTier.Premium
            ? settings.PremiumCustomerMultiplier
            : 1.0;

        var windowHours = Math.Max(
            settings.MinimumWindowHours,
            baseHours * multiplier);

        var newDueAtUtc = now.AddHours(windowHours);

        ticket.SetPriority(nextPriority, now);
        ticket.SetDueAt(newDueAtUtc, now);

        var history = TicketEscalation.Create(
            ticket.Id,
            previousPriority,
            nextPriority,
            previousAgentId,
            ticket.AssignedAgentId,
            previousDueAtUtc,
            newDueAtUtc,
            request.Reason.Trim(),
            request.EscalatedBy.Trim(),
            now);

        await escalations.AddAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await getTicket.HandleAsync(id, ct);
    }
}
