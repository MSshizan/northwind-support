
using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets;

public class TicketTriageServiceTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly FixedClock _clock = new();

    private TicketTriageService CreateService() =>
        new(
            _agents.Object,
            _tickets.Object,
            Options.Create(new SlaOptions()),
            _clock);

    private static Customer CreateCustomer(
        CustomerTier tier = CustomerTier.Standard) =>
        new(
            "Test Customer",
            "customer@example.com",
            null,
            tier,
            FixedClock.DefaultNow);

    private static Category CreateCategory(
        bool requiresSpecialist = false,
        bool forcesCriticalPriority = false) =>
        new("General", requiresSpecialist, forcesCriticalPriority);

    private static Ticket CreateTicket(
        TicketPriority priority = TicketPriority.Medium) =>
        Ticket.Raise(
            "TCK-0001",
            "Test ticket",
            "A description for the test ticket.",
            customerId: 1,
            categoryId: 1,
            priority,
            FixedClock.DefaultNow);

    public TicketTriageServiceTests()
    {
        _agents
            .Setup(a => a.GetAllForAssignmentAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Agent>());

        _tickets
            .Setup(t => t.CountOpenByAgentAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    [Fact]
    public async Task ApplyAsync_PremiumCustomer_GetsHalfTheConfiguredSlaWindow()
    {
        var ticket = CreateTicket();
        var service = CreateService();

        var result = await service.ApplyAsync(
            ticket,
            CreateCustomer(CustomerTier.Premium),
            CreateCategory(),
            CancellationToken.None);

        // Medium has a 24-hour base window; premium customers receive half.
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(12),
            ticket.DueAtUtc);

        Assert.Contains("Premium", result.SlaReason);
        Assert.Contains("12", result.SlaReason);
    }

    [Fact]
    public async Task ApplyAsync_ForcedCriticalCategory_OverridesRequestedPriority()
    {
        var ticket = CreateTicket(TicketPriority.Low);
        var service = CreateService();

        var result = await service.ApplyAsync(
            ticket,
            CreateCustomer(),
            CreateCategory(forcesCriticalPriority: true),
            CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(4),
            ticket.DueAtUtc);

        Assert.Contains("Critical", result.PriorityReason);
    }

    [Fact]
    public async Task ApplyAsync_NoEligibleAgents_LeavesTicketUnassignedWithReason()
    {
        var ticket = CreateTicket();
        var service = CreateService();

        var result = await service.ApplyAsync(
            ticket,
            CreateCustomer(),
            CreateCategory(),
            CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Contains("unassigned", result.AssignmentReason);
    }
}
