
namespace SupportDesk.Application.Contracts.Tickets;

public sealed class TriageDto
{
    public string PriorityReason { get; set; } = string.Empty;

    public string SlaReason { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;
}
