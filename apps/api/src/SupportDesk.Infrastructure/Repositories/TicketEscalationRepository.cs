
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketEscalationRepository(
    SupportDbContext dbContext) : ITicketEscalationRepository
{
    public async Task AddAsync(
        TicketEscalation escalation,
        CancellationToken ct)
    {
        await dbContext.Set<TicketEscalation>().AddAsync(escalation, ct);
    }

    public async Task<IReadOnlyList<TicketEscalation>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken ct)
    {
        return await dbContext.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.EscalatedAtUtc)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
    }
}
