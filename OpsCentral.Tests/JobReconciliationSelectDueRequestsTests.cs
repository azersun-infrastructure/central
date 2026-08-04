using OpsCentral.BackgroundServices;
using OpsCentral.Models.Entities;

namespace OpsCentral.Tests;

public class JobReconciliationSelectDueRequestsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectDueRequests_ExcludesRequestsWithNoTimeout()
    {
        var candidates = new[] { NewRequest(timeoutAtUtc: null) };

        var due = JobReconciliationHostedService.SelectDueRequests(candidates, Now);

        Assert.Empty(due);
    }

    [Fact]
    public void SelectDueRequests_ExcludesRequestsWhoseTimeoutHasNotArrivedYet()
    {
        var candidates = new[] { NewRequest(timeoutAtUtc: Now.AddMinutes(5)) };

        var due = JobReconciliationHostedService.SelectDueRequests(candidates, Now);

        Assert.Empty(due);
    }

    [Fact]
    public void SelectDueRequests_IncludesRequestsAtOrPastTheirTimeout()
    {
        var atTimeout = NewRequest(timeoutAtUtc: Now);
        var pastTimeout = NewRequest(timeoutAtUtc: Now.AddMinutes(-5));

        var due = JobReconciliationHostedService.SelectDueRequests([atTimeout, pastTimeout], Now);

        Assert.Equal(2, due.Count);
        Assert.Contains(atTimeout, due);
        Assert.Contains(pastTimeout, due);
    }

    private static AdActionRequest NewRequest(DateTimeOffset? timeoutAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        ActionType = AdActionType.Unlock.ToString(),
        Input = "test.user",
        DispatchTarget = DispatchTarget.Jenkins.ToString(),
        Status = JobStatus.Running.ToString(),
        TimeoutAtUtc = timeoutAtUtc
    };
}
