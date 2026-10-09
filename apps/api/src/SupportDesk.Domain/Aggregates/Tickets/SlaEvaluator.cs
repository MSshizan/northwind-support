
namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA status from ticket dates. SLA status is never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc) =>
        Evaluate(dueAtUtc, resolvedAtUtc, null, null, nowUtc, 25);

    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime? startedAtUtc,
        TimeSpan? slaWindow,
        DateTime nowUtc,
        double atRiskThresholdPercent = 25)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc
                ? SlaStatus.Met
                : SlaStatus.Breached;
        }

        if (nowUtc >= dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (startedAtUtc is not null &&
            slaWindow is not null &&
            slaWindow.Value > TimeSpan.Zero)
        {
            var remaining = dueAtUtc.Value - nowUtc;
            var threshold = Math.Clamp(atRiskThresholdPercent, 0, 100) / 100.0;
            var atRiskWindow = TimeSpan.FromTicks(
                (long)(slaWindow.Value.Ticks * threshold));

            if (remaining <= atRiskWindow)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }
}
