using OpsCentral.Models.Entities;

namespace OpsCentral.Services;

public interface IAdActionRequestService
{
    Task<AdActionRequest> SubmitActionAsync(
        AdActionType actionType,
        string input,
        string requestedByUpnOrUsername,
        string requestedByAuthSource,
        CancellationToken ct);

    Task<List<AdActionRequest>> GetRecentAsync(int take, CancellationToken ct);

    Task<AdActionRequest?> GetWithEventsAsync(Guid id, CancellationToken ct);
}
