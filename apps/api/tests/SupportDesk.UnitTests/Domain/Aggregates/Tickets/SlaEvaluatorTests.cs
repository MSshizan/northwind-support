using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));


    
    [Fact]
    public void Evaluate_OpenWithMoreThan25PercentRemaining_IsWithinSla()
    {
        var start = Now;
        var window = TimeSpan.FromHours(4);

        Assert.Equal(
            SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(
                start.Add(window),
                null,
                start,
                window,
                start.AddHours(2)));
    }

    [Fact]
    public void Evaluate_Exactly25PercentRemaining_IsAtRisk()
    {
        var start = Now;
        var window = TimeSpan.FromHours(4);

        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                start.Add(window),
                null,
                start,
                window,
                start.AddHours(3)));
    }

    [Fact]
    public void Evaluate_OpenWithLessThan25PercentRemaining_IsAtRisk()
    {
        var start = Now;
        var window = TimeSpan.FromHours(4);

        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(
                start.Add(window),
                null,
                start,
                window,
                start.AddHours(3).AddMinutes(30)));
    }

}
