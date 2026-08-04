namespace OpsCentral.Options;

public class DispatchOptions
{
    public const string SectionName = "Dispatch";

    /// <summary>When true, both DispatchTarget keys resolve to MockAdActionDispatcher instead of the real Jenkins/Azure Automation dispatchers. Default true in Development.</summary>
    public bool UseMock { get; set; }
}
