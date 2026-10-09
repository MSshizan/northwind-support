
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    IOptions<SlaOptions>? slaOptions = null) : ITicketQueries
{
    private SlaOptions SlaSettings => slaOptions?.Value ?? new SlaOptions();

    /// <summary>
    /// Applies all filters before counting, sorting, and pagination.
    /// </summary>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var settings = SlaSettings;

        var tickets = ApplyFilters(TicketsWithLabels(), query);
        tickets = ApplySlaFilter(tickets, query.SlaStatus, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = CalculateSlaStatus(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    x.Ticket.Priority,
                    x.Customer.Tier,
                    now,
                    settings)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(
            items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var settings = SlaSettings;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Email,
                    x.Customer.Phone,
                    x.Customer.Tier),
                Category = new CategoryDto(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = CalculateSlaStatus(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    x.Ticket.Priority,
                    x.Customer.Tier,
                    now,
                    settings)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var settings = SlaSettings;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = CalculateSlaStatus(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    x.Ticket.Priority,
                    x.Customer.Tier,
                    now,
                    settings)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Applies the standard ticket filters. EF Core translates these expressions to SQL.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        if (query.Status.HasValue)
        {
            source = source.Where(
                x => x.Ticket.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(
                x => x.Ticket.Priority == query.Priority.Value);
        }

        if (query.CategoryId.HasValue)
        {
            source = source.Where(
                x => x.Ticket.CategoryId == query.CategoryId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            source = source.Where(
                x => x.Ticket.CustomerId == query.CustomerId.Value);
        }

        if (query.AssignedAgentId.HasValue)
        {
            source = source.Where(
                x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(
                x => x.Ticket.AssignedAgentId == null);
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        return source;
    }

    /// <summary>
    /// Filters by SLA status before counting and pagination.
    /// </summary>
    private IQueryable<TicketWithLabels> ApplySlaFilter(
        IQueryable<TicketWithLabels> source,
        SlaStatus? slaStatus,
        DateTime now)
    {
        if (!slaStatus.HasValue)
        {
            return source;
        }

        var settings = SlaSettings;
        var threshold = Math.Clamp(
            settings.AtRiskThresholdPercent, 0, 100) / 100.0;

        switch (slaStatus.Value)
        {
            case SlaStatus.NotApplicable:
                return source.Where(x => x.Ticket.DueAtUtc == null);

            case SlaStatus.Met:
                return source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    x.Ticket.ResolvedAtUtc != null &&
                    x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc);

            case SlaStatus.Breached:
                return source.Where(x =>
                    x.Ticket.DueAtUtc != null &&
                    (
                        (x.Ticket.ResolvedAtUtc != null &&
                         x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                        (x.Ticket.ResolvedAtUtc == null &&
                         x.Ticket.DueAtUtc <= now)
                    ));

            case SlaStatus.AtRisk:
                return ApplyUnresolvedWindowFilter(
                    source, now, threshold, atRisk: true);

            case SlaStatus.WithinSla:
                return ApplyUnresolvedWindowFilter(
                    source, now, threshold, atRisk: false);

            default:
                return source;
        }
    }

    /// <summary>
    /// Filters unresolved tickets using configured priority windows and customer tier.
    /// </summary>
    private IQueryable<TicketWithLabels> ApplyUnresolvedWindowFilter(
        IQueryable<TicketWithLabels> source,
        DateTime now,
        double threshold,
        bool atRisk)
    {
        var settings = SlaSettings;

        var lowStandard = GetWindowHours(TicketPriority.Low, CustomerTier.Standard, settings);
        var lowPremium = GetWindowHours(TicketPriority.Low, CustomerTier.Premium, settings);
        var mediumStandard = GetWindowHours(TicketPriority.Medium, CustomerTier.Standard, settings);
        var mediumPremium = GetWindowHours(TicketPriority.Medium, CustomerTier.Premium, settings);
        var highStandard = GetWindowHours(TicketPriority.High, CustomerTier.Standard, settings);
        var highPremium = GetWindowHours(TicketPriority.High, CustomerTier.Premium, settings);
        var criticalStandard = GetWindowHours(TicketPriority.Critical, CustomerTier.Standard, settings);
        var criticalPremium = GetWindowHours(TicketPriority.Critical, CustomerTier.Premium, settings);

        var lowStandardCutoff = now.AddHours(lowStandard * threshold);
        var lowPremiumCutoff = now.AddHours(lowPremium * threshold);
        var mediumStandardCutoff = now.AddHours(mediumStandard * threshold);
        var mediumPremiumCutoff = now.AddHours(mediumPremium * threshold);
        var highStandardCutoff = now.AddHours(highStandard * threshold);
        var highPremiumCutoff = now.AddHours(highPremium * threshold);
        var criticalStandardCutoff = now.AddHours(criticalStandard * threshold);
        var criticalPremiumCutoff = now.AddHours(criticalPremium * threshold);

        var activeUnresolved = source.Where(x =>
            x.Ticket.DueAtUtc != null &&
            x.Ticket.ResolvedAtUtc == null &&
            x.Ticket.DueAtUtc > now);

        if (atRisk)
        {
            return activeUnresolved.Where(x =>
                (x.Ticket.Priority == TicketPriority.Low &&
                 ((x.Customer.Tier == CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= lowPremiumCutoff) ||
                  (x.Customer.Tier != CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= lowStandardCutoff))) ||

                (x.Ticket.Priority == TicketPriority.Medium &&
                 ((x.Customer.Tier == CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= mediumPremiumCutoff) ||
                  (x.Customer.Tier != CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= mediumStandardCutoff))) ||

                (x.Ticket.Priority == TicketPriority.High &&
                 ((x.Customer.Tier == CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= highPremiumCutoff) ||
                  (x.Customer.Tier != CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= highStandardCutoff))) ||

                (x.Ticket.Priority == TicketPriority.Critical &&
                 ((x.Customer.Tier == CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= criticalPremiumCutoff) ||
                  (x.Customer.Tier != CustomerTier.Premium &&
                   x.Ticket.DueAtUtc <= criticalStandardCutoff))));
        }

        return activeUnresolved.Where(x =>
            (x.Ticket.Priority == TicketPriority.Low &&
             ((x.Customer.Tier == CustomerTier.Premium &&
               x.Ticket.DueAtUtc > lowPremiumCutoff) ||
              (x.Customer.Tier != CustomerTier.Premium &&
               x.Ticket.DueAtUtc > lowStandardCutoff))) ||

            (x.Ticket.Priority == TicketPriority.Medium &&
             ((x.Customer.Tier == CustomerTier.Premium &&
               x.Ticket.DueAtUtc > mediumPremiumCutoff) ||
              (x.Customer.Tier != CustomerTier.Premium &&
               x.Ticket.DueAtUtc > mediumStandardCutoff))) ||

            (x.Ticket.Priority == TicketPriority.High &&
             ((x.Customer.Tier == CustomerTier.Premium &&
               x.Ticket.DueAtUtc > highPremiumCutoff) ||
              (x.Customer.Tier != CustomerTier.Premium &&
               x.Ticket.DueAtUtc > highStandardCutoff))) ||

            (x.Ticket.Priority == TicketPriority.Critical &&
             ((x.Customer.Tier == CustomerTier.Premium &&
               x.Ticket.DueAtUtc > criticalPremiumCutoff) ||
              (x.Customer.Tier != CustomerTier.Premium &&
               x.Ticket.DueAtUtc > criticalStandardCutoff))));
    }

    /// <summary>
    /// Calculates SLA status for a returned ticket.
    /// </summary>
    private static SlaStatus CalculateSlaStatus(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime now,
        SlaOptions settings)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        var windowHours = GetWindowHours(priority, customerTier, settings);
        var window = TimeSpan.FromHours(windowHours);
        var startedAtUtc = dueAtUtc.Value.Subtract(window);

        return SlaEvaluator.Evaluate(
            dueAtUtc,
            resolvedAtUtc,
            startedAtUtc,
            window,
            now,
            settings.AtRiskThresholdPercent);
    }

    private static double GetWindowHours(
        TicketPriority priority,
        CustomerTier customerTier,
        SlaOptions settings)
    {
        var priorityName = priority.ToString();

        var baseHours = settings.BaseWindowHours.TryGetValue(
            priorityName, out var configuredHours)
            ? configuredHours
            : 24;

        var multiplier = customerTier == CustomerTier.Premium
            ? settings.PremiumCustomerMultiplier
            : 1.0;

        return Math.Max(
            settings.MinimumWindowHours,
            baseHours * multiplier);
    }

    /// <summary>
    /// Joins the ticket with customer, category, and optional agent labels.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),

            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),

            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),

            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),

            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
