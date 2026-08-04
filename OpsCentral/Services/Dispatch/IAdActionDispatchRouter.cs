using OpsCentral.Models.Entities;

namespace OpsCentral.Services.Dispatch;

public interface IAdActionDispatchRouter
{
    DispatchTarget ResolveTarget(AdActionType actionType);

    IAdActionDispatcher ResolveDispatcher(DispatchTarget target);
}
