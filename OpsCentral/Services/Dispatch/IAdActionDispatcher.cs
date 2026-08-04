using OpsCentral.Models.Entities;

namespace OpsCentral.Services.Dispatch;

public interface IAdActionDispatcher
{
    DispatchTarget Target { get; }

    Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct);

    Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct);
}
