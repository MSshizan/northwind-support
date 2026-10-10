
namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed record EscalateTicketCommand(
    string Reason,
    string EscalatedBy);
