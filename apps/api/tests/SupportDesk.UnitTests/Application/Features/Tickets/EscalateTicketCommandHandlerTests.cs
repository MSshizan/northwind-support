
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenPriorityIsLow_EscalatesToMediumAndSavesHistory()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithPriority(TicketPriority.Low).Build());

        var request = new EscalateTicketRequest(
            "Customer impact has increased",
            "Team Lead");

        var result = await context.EscalateTicket.HandleAsync(
            ticket.Id, request, CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.NotNull(result);
        context.Escalations.Verify(
            e => e.AddAsync(
                It.Is<TicketEscalation>(history =>
                    history.TicketId == ticket.Id &&
                    history.FromPriority == TicketPriority.Low &&
                    history.ToPriority == TicketPriority.Medium &&
                    history.Reason == request.Reason &&
                    history.EscalatedBy == request.EscalatedBy),
                It.IsAny<CancellationToken>()),
            Times.Once());

        context.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    public async Task HandleAsync_WhenReasonIsTooShort_ThrowsValidationException(
        string reason)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest(reason, "Team Lead"),
                CancellationToken.None));

        context.VerifySaved(Times.Never());
        context.Escalations.Verify(
            e => e.AddAsync(
                It.IsAny<TicketEscalation>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFoundException()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.EscalateTicket.HandleAsync(
                999,
                new EscalateTicketRequest("Customer impact increased", "Team Lead"),
                CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsAlreadyCritical_ThrowsConflictException()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest("Customer impact increased", "Team Lead"),
                CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsNotOpen_ThrowsConflictException(
        TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        ticket.ChangeStatus(status, context.Clock.UtcNow);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.EscalateTicket.HandleAsync(
                ticket.Id,
                new EscalateTicketRequest("Customer impact increased", "Team Lead"),
                CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
