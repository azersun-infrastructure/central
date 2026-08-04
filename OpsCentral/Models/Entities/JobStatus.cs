namespace OpsCentral.Models.Entities;

public enum JobStatus
{
    Pending,
    Dispatching,
    DispatchFailed,
    Running,
    Succeeded,
    Failed,
    TimedOut
}
